using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    internal static class Mission01EngineAudioAuthoring
    {
        private const string Raw = "Resources/Audio/";
        private const string Live = "Assets/_Project/Art/Aircraft/Audio/";
        private const string Cruise = "128614__smidoid__jetengine-loop.aiff";
        private const string Boost = "146770__qubodup__rocket-boost-engine-loop.wav";

        [MenuItem("Air Combat/Missions/Install Mission01 Engine Audio v0")]
        private static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int group = -1;
            try
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.isLoaded || scene.name != "Mission01_FirstSortie")
                    throw new InvalidOperationException("Open Mission01_FirstSortie first.");
                var launch = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Mission01LaunchSequence>(true)).Single();
                var flight = new SerializedObject(launch).FindProperty("flight").objectReferenceValue as FlightController;
                if (flight == null || flight.GetComponent<Afterburner>() == null)
                    throw new InvalidOperationException("Mission player or afterburner missing.");
                string rootPath = Path.GetDirectoryName(Application.dataPath);
                foreach (string file in new[] { Cruise, Boost })
                    if (!File.Exists(Path.Combine(rootPath, Raw + file)))
                        throw new FileNotFoundException("Missing source audio: " + Raw + file);
                Directory.CreateDirectory(Path.Combine(rootPath, Live));
                var cruise = ImportLoop(rootPath, Cruise, "Lynx_Engine_Cruise.wav");
                // Remove the obsolete generated AIFF (including a failed import).
                string oldPath = Live + "Lynx_Engine_Cruise.aiff";
                if (File.Exists(Path.Combine(rootPath, oldPath)) && !AssetDatabase.DeleteAsset(oldPath))
                    Debug.LogWarning("AUD-01A: remove obsolete generated asset manually: " + oldPath);
                var boost = ImportLoop(rootPath, Boost, "Lynx_Engine_Boost.wav");
                Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Install Mission01 engine audio");
                var audio = launch.GetComponent<Mission01EngineAudio>();
                if (audio == null) audio = Undo.AddComponent<Mission01EngineAudio>(launch.gameObject);
                Undo.RecordObject(audio, "Bind engine audio clips");
                audio.ConfigureForAuthoring(flight, cruise, boost);
                EditorUtility.SetDirty(audio);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = launch.gameObject;
                Debug.Log("AUD-01: Lynx engine mix installed. Tune Mission01EngineAudio in Inspector; save scene and generated assets.", audio);
            }
            catch (Exception ex)
            {
                if (group >= 0) Undo.RevertAllDownToGroup(group);
                Debug.LogError("AUD-01: " + ex);
            }
        }

        private static AudioClip ImportLoop(string root, string source, string name)
        {
            string assetPath = Live + name;
            // Preserve accepted live audio if the menu is run again. Raw sources
            // remain untouched and are not runtime dependencies.
            string destination = Path.Combine(root, assetPath);
            if (!File.Exists(destination))
            {
                string rawPath = Path.Combine(root, Raw + source);
                if (Path.GetExtension(source).Equals(".aiff", StringComparison.OrdinalIgnoreCase))
                    ConvertAiffToWav(rawPath, destination);
                else File.Copy(rawPath, destination);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Audio import failed: " + assetPath);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null || clip.length <= 0f) throw new InvalidOperationException("Invalid clip: " + assetPath);
            return clip;
        }

        // Decode uncompressed AIFF/AIFC directly; no ffmpeg or external tools.
        // Output standard little-endian 16-bit PCM WAV for Unity's importer.
        private static void ConvertAiffToWav(string source, string destination)
        {
            byte[] data = File.ReadAllBytes(source);
            if (data.Length < 12 || FourCC(data, 0) != "FORM")
                throw new InvalidDataException("Not an AIFF FORM: " + source);
            string form = FourCC(data, 8);
            if (form != "AIFF" && form != "AIFC")
                throw new InvalidDataException("Unsupported audio FORM: " + form);
            int channels = 0, bits = 0, rate = 0, sound = -1, soundBytes = 0;
            uint frames = 0;
            string codec = "NONE";
            bool hasComm = false;
            long end = 8L + Big32(data, 4);
            if (end > data.Length || end < 12) throw new InvalidDataException("Truncated AIFF FORM.");
            for (long pos = 12; pos + 8 <= end;)
            {
                int at = (int)pos;
                string id = FourCC(data, at);
                uint size = Big32(data, at + 4);
                long next = pos + 8 + size;
                if (next > end) throw new InvalidDataException("Truncated AIFF chunk: " + id);
                int body = at + 8;
                if (id == "COMM")
                {
                    if (size < (form == "AIFC" ? 22 : 18)) throw new InvalidDataException("Invalid AIFF COMM.");
                    channels = Big16(data, body);
                    frames = Big32(data, body + 2);
                    bits = Big16(data, body + 6);
                    rate = ReadSampleRate(data, body + 8);
                    if (form == "AIFC") codec = FourCC(data, body + 18);
                    hasComm = true;
                }
                else if (id == "SSND")
                {
                    if (size < 8) throw new InvalidDataException("Invalid AIFF SSND.");
                    uint offset = Big32(data, body);
                    if (offset > size - 8) throw new InvalidDataException("Invalid AIFF sound offset.");
                    sound = (int)(body + 8L + offset);
                    soundBytes = (int)(size - 8 - offset);
                }
                pos = next + (size & 1u);
            }
            bool little = codec == "sowt";
            bool float32 = codec == "fl32" || codec == "FL32";
            bool float64 = codec == "fl64" || codec == "FL64";
            bool pcm = codec == "NONE" || codec == "twos" || little;
            if (!hasComm || sound < 0 || channels < 1 || channels > 2 || frames == 0 || rate < 1 || rate > 384000)
                throw new InvalidDataException("Missing or invalid AIFF audio metadata (mono/stereo only).");
            if (!(pcm && (bits == 8 || bits == 16 || bits == 24 || bits == 32)) &&
                !(float32 && bits == 32) && !(float64 && bits == 64))
                throw new InvalidDataException("Unsupported AIFF encoding: " + codec + " / " + bits + " bit. Convert source to PCM WAV externally.");
            int bytes = bits / 8;
            long samples = (long)frames * channels;
            if (samples * bytes > soundBytes || samples * 2 > int.MaxValue - 44)
                throw new InvalidDataException("Truncated or oversized AIFF audio.");
            using (var output = new MemoryStream((int)(44 + samples * 2)))
            using (var writer = new BinaryWriter(output))
            {
                int payload = (int)(samples * 2);
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + payload);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((ushort)1); writer.Write((ushort)channels); writer.Write(rate);
                writer.Write(rate * channels * 2); writer.Write((ushort)(channels * 2)); writer.Write((ushort)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(payload);
                for (long i = 0; i < samples; i++)
                {
                    int at = sound + (int)(i * bytes);
                    double value;
                    if (float32) value = BitConverter.Int32BitsToSingle(unchecked((int)Big32(data, at)));
                    else if (float64)
                    {
                        ulong raw = ((ulong)Big32(data, at) << 32) | Big32(data, at + 4);
                        value = BitConverter.Int64BitsToDouble(unchecked((long)raw));
                    }
                    else
                    {
                        uint raw = 0;
                        for (int b = 0; b < bytes; b++)
                            raw = (raw << 8) | data[at + (little ? bytes - 1 - b : b)];
                        int signed = unchecked((int)(raw << (32 - bits))) >> (32 - bits);
                        value = signed / Math.Pow(2, bits - 1);
                    }
                    if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
                    value = Math.Max(-1.0, Math.Min(1.0, value));
                    writer.Write((short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(value * 32768.0))));
                }
                writer.Flush();
                File.WriteAllBytes(destination, output.ToArray());
            }
            Debug.Log($"AUD-01A: converted {codec} {bits}-bit AIFF to PCM16 WAV ({rate} Hz, {channels} ch, {frames} frames).");
        }

        private static string FourCC(byte[] data, int at) => Encoding.ASCII.GetString(data, at, 4);
        private static int Big16(byte[] data, int at) => (data[at] << 8) | data[at + 1];
        private static uint Big32(byte[] data, int at) => ((uint)data[at] << 24) |
            ((uint)data[at + 1] << 16) | ((uint)data[at + 2] << 8) | data[at + 3];

        private static int ReadSampleRate(byte[] data, int at)
        {
            // AIFF stores sample rate in 80-bit extended precision.
            int exponent = Big16(data, at);
            if ((exponent & 0x8000) != 0 || exponent == 0x7fff || exponent == 0)
                throw new InvalidDataException("Invalid AIFF sample rate.");
            ulong mantissa = ((ulong)Big32(data, at + 2) << 32) | Big32(data, at + 6);
            double rate = mantissa * Math.Pow(2, exponent - 16383 - 63);
            if (rate < 1 || rate > 384000) throw new InvalidDataException("Unsupported AIFF sample rate: " + rate);
            return (int)Math.Round(rate);
        }
    }
}
