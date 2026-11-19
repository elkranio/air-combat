using System;
using UnityEngine;

namespace AirCombat
{
    // Shared runtime materials: each chunk uses the same palette, not its own instances.
    internal static class LandscapePalette
    {
        public const int Grass = 0;
        public const int LightGrass = 1;
        public const int DarkGrass = 2;
        public const int Meadow = 3;
        public const int LightMeadow = 4;
        public const int Bank = 5;
        public const int Water = 6;
        public const int Pine = 7;
        public const int PineLight = 8;
        public const int Leaf = 9;
        public const int LeafLight = 10;
        public const int Bark = 11;
        public const int Stone = 12;
        public const int Count = 13;

        public static Material[] Create()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Landscape requires the URP Lit shader.");

            return new[]
            {
                Make(shader, "Grass", 112, 142, 101),
                Make(shader, "Light grass", 119, 148, 107),
                Make(shader, "Dark grass", 101, 130, 91),
                Make(shader, "Meadow", 142, 151, 103),
                Make(shader, "Light meadow", 150, 159, 110),
                Make(shader, "River bank", 158, 153, 119),
                Make(shader, "Water", 83, 143, 162),
                Make(shader, "Pine", 57, 98, 77),
                Make(shader, "Light pine", 73, 117, 87),
                Make(shader, "Leaf", 79, 116, 72),
                Make(shader, "Light leaf", 101, 137, 78),
                Make(shader, "Bark", 110, 96, 75),
                Make(shader, "Stone", 126, 124, 110)
            };
        }

        private static Material Make(Shader shader, string name, byte r, byte g, byte b)
        {
            var material = new Material(shader) { name = "Landscape / " + name };
            material.SetColor("_BaseColor", new Color32(r, g, b, 255));
            material.SetFloat("_Smoothness", 0f);
            return material;
        }
    }
}
