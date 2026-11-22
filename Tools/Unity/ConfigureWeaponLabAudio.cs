using System;
using System.IO;
using System.Linq;
using System.Text;
using AirCombat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// WeaponLab only. Extract compact, playable shots from the raw CC0 pack without
// moving its sources into Assets or copying multi-second tails into Git twice.
public static class ConfigureWeaponLabAudio
{
    private const string ScenePath = "Assets/_Project/Scenes/WeaponLab.unity";
    private const string Raw = "Resources/Audio/military_sfx_pack_v0.2/machine_guns/mg";
    private const string Output = "Assets/_Project/Art/Weapons/Audio";
    private const string Staging = "Assets/_Project/Generated/WeaponLab/_AudioImportStaging.wav";
    private const int Count = 4;

    public static string Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before installing Weapon Lab audio.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save open scenes before installing Weapon Lab audio.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Build WeaponLab.unity first.");
        if (!File.Exists("Resources/Audio/military_sfx_pack_v0.2/SOURCES_CC0.txt"))
            throw new InvalidOperationException("Missing source pack license / SOURCES_CC0.txt.");
        if (!AssetDatabase.IsValidFolder(Output) || !AssetDatabase.IsValidFolder("Assets/_Project/Generated/WeaponLab"))
            throw new InvalidOperationException("Weapon Lab audio or generated folder is missing.");

        var clips = new AudioClip[Count];
        try
        {
            for (int i = 0; i < Count; i++)
            {
                string input = Raw + "/mg_single_near_" + (i + 1).ToString("00") + ".wav";
                if (!File.Exists(input)) throw new FileNotFoundException("Missing raw CC0 WAV", input);
                // Unity's importer handles WAV encodings; do not maintain a second WAV decoder.
                AssetDatabase.DeleteAsset(Staging);
                File.Copy(input, Staging);
                AssetDatabase.ImportAsset(Staging, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(Staging) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Could not import WAV: " + input);
                importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    preloadAudioData = true,
                    compressionFormat = AudioCompressionFormat.PCM,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate
                };
                importer.SaveAndReimport();
                var rawClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Staging);
                if (rawClip == null || rawClip.channels != 1)
                    throw new InvalidOperationException("Staged audio must be mono PCM: " + input);
                rawClip.LoadAudioData();
                var samples = new float[rawClip.samples];
                if (!rawClip.GetData(samples, 0))
                    throw new InvalidOperationException("Could not read decompressed WAV: " + input);
                string destination = Output + "/Autocannon_MG_" + (i + 1).ToString("00") + ".wav";
                var wav = ExtractShot(samples, rawClip.frequency, input);
                if (!File.Exists(destination) || !File.ReadAllBytes(destination).SequenceEqual(wav))
                    File.WriteAllBytes(destination, wav);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                var destImporter = AssetImporter.GetAtPath(destination) as AudioImporter;
                if (destImporter == null) throw new InvalidOperationException("Could not import processed clip: " + destination);
                destImporter.forceToMono = true;
                destImporter.loadInBackground = false;
                destImporter.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    preloadAudioData = true,
                    compressionFormat = AudioCompressionFormat.PCM,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate
                };
                destImporter.SaveAndReimport();
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(destination);
                if (clips[i] == null) throw new InvalidOperationException("Processed WAV is missing: " + destination);
            }
        }
        finally
        {
            // Temporary long raw sound must never become a committed Unity asset.
            AssetDatabase.DeleteAsset(Staging);
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = scene.GetRootGameObjects().SingleOrDefault(go => go.name == "Player");
        var gun = player != null ? player.GetComponent<WeaponLabCannon>() : null;
        if (gun == null) throw new InvalidOperationException("WeaponLab scene has no player autocannon.");
        gun.ConfigureSoundVariants(clips);
        EditorUtility.SetDirty(gun);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save WeaponLab audio setup.");
        Selection.activeGameObject = player;
        return "Weapon Lab audio installed: G cycles SYNTH / MG 01 / MG 02 / MG 03 / MG 04. " +
            "Raw files untouched; four compact processed WAVs in " + Output;
    }

    private static byte[] ExtractShot(float[] original, int frequency, string filename)
    {
        if (frequency < 8000 || original.Length < frequency / 20)
            throw new InvalidOperationException("Invalid raw WAV: " + filename);
        // Find the earliest significant transient within the first two seconds,
        // instead of assuming every source starts at sample zero.
        int search = Math.Min(original.Length, frequency * 2);
        float peak = 0f;
        for (int i = 0; i < search; i++) peak = Mathf.Max(peak, Mathf.Abs(original[i]));
        if (peak < .01f) throw new InvalidOperationException("No audible attack found in: " + filename);
        float threshold = Mathf.Max(.012f, peak * .16f);
        int onset = 0;
        int block = Math.Max(1, frequency / 200);
        for (int i = 0; i < search; i += block)
        {
            float local = 0f;
            for (int k = i; k < Math.Min(search, i + block); k++)
                local = Mathf.Max(local, Mathf.Abs(original[k]));
            if (local < threshold) continue;
            onset = i;
            break;
        }
        int start = Math.Max(0, onset - Mathf.RoundToInt(.012f * frequency));
        int length = Math.Min(Mathf.RoundToInt(.34f * frequency), original.Length - start);
        if (length <= 0) throw new InvalidOperationException("WAV has no sound after onset: " + filename);
        float samplePeak = 0f;
        for (int i = 0; i < length; i++) samplePeak = Mathf.Max(samplePeak, Mathf.Abs(original[start + i]));
        float gain = samplePeak > 0f ? Mathf.Min(2.2f, .88f / samplePeak) : 1f;
        int fade = Math.Min(length, Mathf.RoundToInt(.065f * frequency));

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((ushort)1); // PCM
            writer.Write((ushort)1); // mono
            writer.Write(frequency);
            writer.Write(frequency * 2);
            writer.Write((ushort)2);
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(length * 2);
            for (int i = 0; i < length; i++)
            {
                float envelope = i >= length - fade ? (length - i - 1f) / Math.Max(1, fade) : 1f;
                float s = Mathf.Clamp(original[start + i] * gain * envelope, -1f, 1f);
                writer.Write((short)Mathf.RoundToInt(s * 32767f));
            }
            return stream.ToArray();
        }
    }
}
