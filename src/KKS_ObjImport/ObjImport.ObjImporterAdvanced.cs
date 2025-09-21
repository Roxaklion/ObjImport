﻿/// "borrowed" from https://github.com/aedenthorn/ValheimMods/blob/master/CustomMeshes
/// modified to create mutliple meshes if the .obj has multiple objects.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace ObjImport
{
    public class ObjImporterAdvanced
    {

        private ManualLogSource Logger;

        public ObjImporterAdvanced(ManualLogSource Logger)
        {
            this.Logger = Logger;
        }

        public List<Mesh> convertToMesh(List<MeshAdvancedDto> data, bool is32bit)
        {
            List<Mesh> meshes = new List<Mesh>();

            for (int ii = 0; ii < data.Count; ii++)
            {
                MeshAdvancedDto newMesh = data[ii];
                populateMeshStruct(ref newMesh);
                Vector3[] newVerts = new Vector3[newMesh.faceData.Length];
                Vector2[] newUVs = new Vector2[newMesh.faceData.Length];
                Vector3[] newNormals = new Vector3[newMesh.faceData.Length];
                /* The following foreach loops through the facedata and assigns the appropriate vertex, uv, or normal
                 * for the appropriate Unity mesh array.
                 */
                int i = 0;
                foreach (Vector3 v in newMesh.faceData)
                {
                    newVerts[i] = newMesh.vertices[(int)v.x - 1];
                    if (v.y >= 1)
                        newUVs[i] = newMesh.uv[(int)v.y - 1];

                    if (v.z >= 1)
                    {
                        newNormals[i] = newMesh.normals[(int)v.z - 1];
                    }
                    i++;
                }

                if (is32bit)
                {
                    if (newVerts.Length > 65000)
                    {
                        Logger.LogMessage($"ERROR: Mesh [{data[ii].name}] is too big (>65k vertices)!");
                        return null;
                    }
                }

                Mesh mesh = new Mesh();
                mesh.name = data[ii].name;
                //mesh.name = newMesh.name;
                mesh.vertices = newVerts;
                mesh.uv = newUVs;
                mesh.normals = newNormals;
                mesh.triangles = newMesh.triangles;

                mesh.RecalculateBounds();
                //mesh.Optimize();
                meshes.Add(mesh);
            }
            return meshes;
        }

        // Use this for initialization
        public List<MeshAdvancedDto> ImportFile(string filePath)
        {
            try
            {
                usedUpVerts = 0;
                usedUpNorms = 0;
                usedUpUVs = 0;
                List<MeshAdvancedDto> newMeshes = createMeshStruct(filePath);
                if (newMeshes == null)
                {
                    Logger.LogError("Error in creating mesh structures.");
                    return null;
                }

                return newMeshes;
            }
            catch (Exception error)
            {
                Logger.LogError($"And error occured on importing the obj: {error}");
                return null;
            }
        }

        private List<MeshAdvancedDto> createMeshStruct(string filename)
        {

            List<MeshAdvancedDto> meshStructs = new List<MeshAdvancedDto>();
            int triangles = 0;
            int vertices = 0;
            int vt = 0;
            int vn = 0;
            int face = 0;
            int line = 0;
            int startLine = 0;
            int endLine = 0;
            string mtlFile = null;

            MeshAdvancedDto mesh = new MeshAdvancedDto();
            mesh.fileName = filename;
            string usedMaterial = "default";

            StreamReader stream = File.OpenText(filename);
            string entireText = stream.ReadToEnd();
            stream.Close();

            using (StringReader reader = new StringReader(entireText))
            {
                string currentText = reader.ReadLine();
                char[] splitIdentifier = { ' ' };
                string[] brokenString;
                while (currentText != null)
                {
                    if (currentText.StartsWith("mtllib "))
                    {
                        //Logger.LogError("mtllib");
                        mtlFile = currentText.Substring(7).Trim();
                        line++;
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                    else if (currentText.StartsWith("usemtl "))
                    {
                        //Logger.LogError("usemtl");
                        usedMaterial = currentText.Substring(7).Trim();
                        line++;
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                    else if (!currentText.StartsWith("f ") && !currentText.StartsWith("v ") && !currentText.StartsWith("vt ")
                        && !currentText.StartsWith("vn ") && !currentText.StartsWith("o "))
                    {
                        currentText = reader.ReadLine();
                        line++;
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                    else
                    {
                        currentText = currentText.Trim();                           //Trim the current line
                        brokenString = currentText.Split(splitIdentifier);      //Split the line into an array, separating the original line by blank spaces
                        switch (brokenString[0])
                        {
                            case "v":
                                vertices++;
                                break;
                            case "vt":
                                vt++;
                                break;
                            case "vn":
                                vn++;
                                break;
                            case "f":
                                if (brokenString.Length > 1)
                                {
                                    face = face + brokenString.Length - 1;
                                    triangles = triangles + 3 * (brokenString.Length - 2); /*brokenString.Length is 3 or greater since a face must have at least
                                                                                     3 vertices.  For each additional vertice, there is an additional
                                                                                     triangle in the mesh (hence this formula).*/
                                }
                                break;
                            case "o":                               //create a new mesh whenever a new object starts.
                                endLine = line;
                                finishMesh();
                                break;
                        }
                        currentText = reader.ReadLine();
                        line++;
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                }
            }
            void finishMesh()
            {
                if (vertices > 0)
                {
                    mesh.triangles = new int[triangles];
                    mesh.vertices = new Vector3[vertices];
                    mesh.uv = new Vector2[vt];
                    mesh.normals = new Vector3[vn];
                    mesh.faceData = new Vector3[face];
                    mesh.startLine = startLine;
                    mesh.usedMaterial = usedMaterial;
                    mesh.mtlFile = mtlFile;
                    meshStructs.Add(mesh);


                    triangles = 0;
                    vertices = 0;
                    vt = 0;
                    vn = 0;
                    face = 0;
                    startLine = endLine;

                    mesh = new MeshAdvancedDto();
                    mesh.fileName = filename;

                }
            }
            finishMesh();
            return meshStructs;
        }

        // I have to subtract this from the number that the obj saves for each vertex of a face
        // because the obj refers to its entire list and I only have the verts of the current object
        private static int usedUpVerts = 0;
        private static int usedUpNorms = 0;
        private static int usedUpUVs = 0;

        private static void populateMeshStruct(ref MeshAdvancedDto mesh)
        {
            StreamReader stream = File.OpenText(mesh.fileName);
            foreach (int ii in Enumerable.Range(0, mesh.startLine))
            {
                stream.ReadLine();
            }
            string objectText = stream.ReadToEnd();
            stream.Close();

            using (StringReader reader = new StringReader(objectText))
            {
                string currentText = reader.ReadLine();

                char[] splitIdentifier = { ' ' };
                char[] splitIdentifier2 = { '/' };
                string[] brokenString;
                string[] brokenBrokenString;
                int f = 0;
                int f2 = 0;
                int v = 0;
                int vn = 0;
                int vt = 0;
                int vt1 = 0;
                int vt2 = 0;
                bool skippedFirstO = false;
                while (currentText != null)
                {
                    if (!currentText.StartsWith("f ") && !currentText.StartsWith("v ") && !currentText.StartsWith("vt ") &&
                        !currentText.StartsWith("vn ") && !currentText.StartsWith("g ") && !currentText.StartsWith("usemtl ") &&
                        !currentText.StartsWith("mtllib ") && !currentText.StartsWith("vt1 ") && !currentText.StartsWith("vt2 ") &&
                        !currentText.StartsWith("vc ") && !currentText.StartsWith("usemap ") && !currentText.StartsWith("o "))
                    {
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                    else
                    {
                        currentText = currentText.Trim();
                        brokenString = currentText.Split(splitIdentifier);
                        switch (brokenString[0])
                        {
                            case "g":
                                break;
                            case "usemtl":
                                break;
                            case "usemap":
                                break;
                            case "mtllib":
                                break;
                            case "v":
                                mesh.vertices[v] = new Vector3(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]),
                                                         System.Convert.ToSingle(brokenString[3]));
                                v++;
                                break;
                            case "vt":
                                mesh.uv[vt] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt++;
                                break;
                            case "vt1":
                                mesh.uv[vt1] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt1++;
                                break;
                            case "vt2":
                                mesh.uv[vt2] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt2++;
                                break;
                            case "vn":
                                mesh.normals[vn] = new Vector3(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]),
                                                        System.Convert.ToSingle(brokenString[3]));
                                vn++;
                                break;
                            case "vc":
                                break;
                            case "f":
                                int j = 1;
                                List<int> intArray = new List<int>();
                                while (j < brokenString.Length && ("" + brokenString[j]).Length > 0)
                                {
                                    Vector3 temp = new Vector3();
                                    brokenBrokenString = brokenString[j].Split(splitIdentifier2, 3);    //Separate the face into individual components (vert, uv, normal)
                                    temp.x = System.Convert.ToInt32(brokenBrokenString[0]) - usedUpVerts; //subtract number of vertieces used by other objects before
                                    if (brokenBrokenString.Length > 1)                                  //Some .obj files skip UV and normal
                                    {
                                        if (brokenBrokenString[1] != "")                                //Some .obj files skip the uv and not the normal
                                        {
                                            temp.y = System.Convert.ToInt32(brokenBrokenString[1]) - usedUpUVs; //subtract number of UVs used by other objects before
                                        }
                                        if (brokenBrokenString.Length > 2)                              //Some .obj files miss the normal completly
                                        {
                                            temp.z = System.Convert.ToInt32(brokenBrokenString[2]) - usedUpNorms; //subtract number of Normals used by other objects before
                                        }
                                    }
                                    j++;

                                    mesh.faceData[f2] = temp;
                                    intArray.Add(f2);
                                    f2++;
                                }
                                j = 1;
                                while (j + 2 < brokenString.Length)     //Create triangles out of the face data.  There will generally be more than 1 triangle per face.
                                {
                                    mesh.triangles[f] = intArray[0];
                                    f++;
                                    mesh.triangles[f] = intArray[j];
                                    f++;
                                    mesh.triangles[f] = intArray[j + 1];
                                    f++;

                                    j++;
                                }
                                break;
                            case "o":
                                if (skippedFirstO)
                                {
                                    usedUpVerts += v; //add the amount of vertices this object has to the usedUpVerts
                                    usedUpNorms += vn; //add the amount of normals this object has to the usedUpNorms
                                    usedUpUVs += vt; //add the amount of UVs this object has to the usedUpUVs
                                    return;
                                }
                                else
                                {
                                    mesh.name = brokenString[1];
                                    skippedFirstO = true;
                                }
                                break;
                        }
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");       //Some .obj files insert double spaces, this removes them.
                        }
                    }
                }
            }
        }
    }
}