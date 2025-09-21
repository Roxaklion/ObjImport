using System.Collections.Generic;
using SysDiag = System.Diagnostics;
using System.IO;
using UnityEngine;
using System;
using static ObjImport.ObjImporter;
using BepInEx.Logging;

namespace ObjImport
{
    public class MtlData
    {
        public string name;
        public string diffuseTexPath;
        public Color diffuseColor = Color.white;
        public Texture2D texture;
    }

    public class MaterialImporter : MonoBehaviour
    {
        
        public Dictionary<string, MtlData> meshMaterialMap = new Dictionary<string, MtlData>();
        private ManualLogSource Logger;

        public MaterialImporter(ManualLogSource Logger)
        {
            this.Logger = Logger;
            this.meshMaterialMap = new Dictionary<string, MtlData>();
        }

        public MtlData findMaterials(string key)
        {
            if (meshMaterialMap.ContainsKey(key)) 
            {
                return meshMaterialMap[key];
            }
            return null;
        }


        public void importMaterials(string filePath, List<MeshAdvancedDto> datas)
        {
            foreach (MeshAdvancedDto data in datas)
            {
                importMaterials(filePath, data);
            }
        }

        public void importMaterials(string filePath, MeshDto data)
        {
            string mtlPath = Path.Combine(Path.GetDirectoryName(filePath), data.mtlFile);
            if (File.Exists(mtlPath))
            {
                LoadMtl(mtlPath);
            }
        }

        // Load MTL file and create Unity materials
        private void LoadMtl(string mtlPath)
        {
            MtlData current = null;
            foreach (var line in File.ReadAllLines(mtlPath))
            {
                string l = line.Trim();
                if (l.StartsWith("newmtl "))
                {
                    current = new MtlData();
                    current.name = l.Substring(7).Trim();
                    if (!this.meshMaterialMap.ContainsKey(current.name))
                    {
                        this.meshMaterialMap[current.name] = current;
                    }
                }
                else if (l.StartsWith("Kd ")) // diffuse color
                {
                    string[] parts = l.Split(' ');
                    if (parts.Length >= 4)
                    {
                        float r = float.Parse(parts[1]);
                        float g = float.Parse(parts[2]);
                        float b = float.Parse(parts[3]);
                        current.diffuseColor = new Color(r, g, b);
                    }
                }
                else if (l.StartsWith("map_Kd ")) // diffuse texture
                {
                    string texFile = l.Substring(7).Trim();
                    string texPath = Path.Combine(Path.GetDirectoryName(mtlPath), texFile);
                    current.diffuseTexPath = texPath;
                }
            }

            //Logger.LogMessage("MtlPath: " + mtlPath);
            foreach (MtlData data in this.meshMaterialMap.Values)
            {
                if (!string.IsNullOrEmpty(data.name))
                {
                    data.texture = LoadTexture(data);
                }

                string materialKey = data.name;
                //Logger.LogMessage("Registerd Materials of Mesh with name: " + materialKey);
                //Logger.LogMessage("matData.name: " + data.name);
                //Logger.LogMessage("matData.diffuseTexPath: " + data.diffuseTexPath);
                //Logger.LogMessage("matData.loadedMaterial.mainTexture: " + data.texture);
                //Register materials to memory map, not yet to GameObject! (will happen during remesh)
            }

        }

        private Texture2D LoadTexture(MtlData data)
        {
            if (!string.IsNullOrEmpty(data.diffuseTexPath) && File.Exists(data.diffuseTexPath))
            {
                byte[] fileData = File.ReadAllBytes(data.diffuseTexPath);
                Texture2D tex = KKAPI.Utilities.TextureUtils.LoadTexture(fileData);
                return tex;
            }
            return null;
        }

        private Material MakeMaterial(MtlData data)
        {
            // Try to find Koikatsu's common item shader first
            Shader shader = Shader.Find("Shader Forge/main_Item_studio_alpha");
            if (shader == null)
                shader = Shader.Find("Standard"); // fallback


            Material mat = new Material(shader);
            mat.name = data.name;

            // Apply diffuse color
            mat.color = data.diffuseColor;

            // Apply texture if present
            mat.mainTexture = data.texture;
            
            return mat;
        }
    }
}