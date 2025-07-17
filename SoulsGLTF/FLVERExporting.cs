using System.Numerics;
using System.Text.Json.Nodes;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;
using SoulsGLTF.Havok;
using SoulsGLTF.Util;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;

namespace SoulsGLTF;

public static class FLVERExporting
{
    [Flags]
    private enum VertexDataFlags
    {
        Empty = 0x0,
        Position = 0x1,
        Normal = 0x2,
        Tangent = 0x4,
        Color0 = 0x8,
        Color1 = 0x10,
        TexCoord0 = 0x20,
        TexCoord1 = 0x40,
        TexCoord2 = 0x80,
        TexCoord3 = 0x100,
        TexCoord4 = 0x200,
        TexCoord5 = 0x400,
        Joints = 0x800,
        SingleJoint = 0x1600,
    }

    private static void FixFlverBoneNodesUsingHkaSkeletonHierarchy(FLVER2 flver, hkaSkeleton skeleton)
    {
        // Reset bone node relations
        foreach (string boneName in skeleton.Bones.Select(x => x.Name))
        {
            FLVER.Node? boneNode = flver.Nodes.FirstOrDefault(x => x.Name == boneName);
            if (boneNode == null)
            {
                continue;
            }
            boneNode.ParentIndex = -1;
            boneNode.FirstChildIndex = -1;
            boneNode.NextSiblingIndex = -1;
            boneNode.PreviousSiblingIndex = -1;
        }
                
        for (int i = 0; i < skeleton.Bones.Length; i++)
        {
            FLVER.Node? boneNode = flver.Nodes.FirstOrDefault(x => x.Name == skeleton.Bones[i].Name);
            if (boneNode == null)
            {
                boneNode = new FLVER.Node()
                {
                    Name = skeleton.Bones[i].Name
                };
                flver.Nodes.Add(boneNode);
            }
            short boneIndex = (short)flver.Nodes.IndexOf(boneNode);
            if (i > 0)
            {
                string parentBoneName = skeleton.Bones[skeleton.ParentIndices[i]].Name;
                FLVER.Node parentNode = flver.Nodes.First(x => x.Name == parentBoneName);
                boneNode.ParentIndex = (short)flver.Nodes.IndexOf(parentNode);
                
                if (parentNode.FirstChildIndex == -1)
                {
                    parentNode.FirstChildIndex = boneIndex;
                }
                else
                {
                    AddNodeToParentRecursive(flver, boneIndex, parentNode.FirstChildIndex);
                }
            }
        }
    }
    
    private static void AddNodeToParentRecursive(FLVER2 flver, short childBoneIndex, short siblingBoneIndex)
    {
        FLVER.Node siblingBoneNode = flver.Nodes[siblingBoneIndex];
        if (siblingBoneNode.NextSiblingIndex == -1)
        {
            siblingBoneNode.NextSiblingIndex = childBoneIndex;
            FLVER.Node childBoneNode = flver.Nodes[childBoneIndex];
            childBoneNode.PreviousSiblingIndex = siblingBoneIndex;
        }
        else
        {
            AddNodeToParentRecursive(flver, childBoneIndex, siblingBoneNode.NextSiblingIndex);
        }
    }
    
    public static void ExportFLVERToGLTF(FLVER2 flver, hkaSkeleton? skeleton, List<hkaAnimation> animations, string fileName, bool binaryOutput = false, bool consolidateBuffers = false)
    {
        FLVER2MaterialInfoBank matInfoBank = new FLVER2MaterialInfoBank();//FLVER2MaterialInfoBank.ReadFromXML("Resources/BankDS3.xml");
        
        bool isSkinned = false;
        AffineTransform masterTransform = AffineTransform.Identity;
        
        if (skeleton != null)
        {
            isSkinned = true;
                
            FixFlverBoneNodesUsingHkaSkeletonHierarchy(flver, skeleton);
                
            // Get master bone transform
            FLVER.Node masterNode = flver.Nodes.First(x => x.Name.Equals("Master", StringComparison.OrdinalIgnoreCase));
                
            Matrix4x4 rotationMatrix =  Matrix4x4.CreateRotationX(masterNode.Rotation.X) *
                                        Matrix4x4.CreateRotationZ(masterNode.Rotation.Z) * 
                                        Matrix4x4.CreateRotationY(masterNode.Rotation.Y);

            AffineTransform fixedTransform = GetTransformationMatrix(masterNode.Translation, masterNode.Rotation, masterNode.Scale);
            
            masterTransform = new (Vector3.One, 
                Quaternion.CreateFromRotationMatrix(rotationMatrix), Math.Abs(fixedTransform.Translation.Z) < 0.000001 ? fixedTransform.Translation : masterNode.Translation);
        }
        
        ModelRoot model = ModelRoot.CreateModel();
        Scene scene = model.UseScene(fileName);
        model.DefaultScene = scene;
        
        Dictionary<short, short> flverNodeToGlNodeMap = new Dictionary<short, short>();
        
        short skeletonIndex = -1;
        List<short> jointIndices = new List<short>(); 

        for (short i = 0; i < flver.Nodes.Count; i++)
        {
            FLVER.Node flvNode = flver.Nodes[i];

            if (flver.Nodes[i].ParentIndex == -1)
            {
                Node glNode = scene.CreateNode(flvNode.Name);
                //Node glNode = model.CreateLogicalNode();
                //glNode.Name = flvNode.Name;
                flverNodeToGlNodeMap.Add(i, (short)glNode.LogicalIndex);

                if (flvNode.Name != "Master")
                {
                    glNode.LocalTransform = GetTransformationMatrix(flvNode.Translation, flvNode.Rotation, flvNode.Scale);
                }

                // Define node as skeleton root if is a skinned mesh
                if (isSkinned)
                {
                    if (flvNode.Name.Equals("Master", StringComparison.OrdinalIgnoreCase))
                    {
                        skeletonIndex = i;
                        jointIndices.Add(i);
                        glNode.LocalTransform = GetTransformationMatrix(Vector3.Zero, flvNode.Rotation, Vector3.One);
                    }
                }
                                    
                if (flvNode.FirstChildIndex > -1)
                {
                    AddNodeToHierarchy(flver, flvNode.FirstChildIndex, glNode, flverNodeToGlNodeMap, i == skeletonIndex ? skeleton : null, jointIndices);
                }
            }
        }

        Skin? skin = null;
        if (isSkinned)
        {
            skin = model.CreateSkin();

            if (jointIndices.Count > 0)
            {
                Node skeletonNode = model.LogicalNodes[flverNodeToGlNodeMap[skeletonIndex]];
                skin.Name = skeletonNode.Name;

                Node[] jointNodes = new Node[jointIndices.Count];
                for (short i = 0; i < jointIndices.Count; i++)
                {
                    jointNodes[i] = model.LogicalNodes[flverNodeToGlNodeMap[jointIndices[i]]];
                }

                skin.BindJoints(jointNodes);
                skin.Skeleton = skeletonNode;
            }
        }
        
        for (int n = 0; n < flver.Nodes.Count; n++)
        {
            List<FLVER2.Mesh> nodeMeshes = flver.Meshes.Where(x => x.NodeIndex == n).ToList();
            
            if (nodeMeshes.Count > 0)
            {
                model.CreateGLTFMeshFromFlverMesh(flver, nodeMeshes, masterTransform.Matrix, matInfoBank, 
                    jointIndices, isSkinned);
                if (skin != null && skin.Joints.Contains(model.LogicalNodes[n]))
                {
                    Node newNode = model.CreateLogicalNode();
                    newNode.Name = model.LogicalNodes[n].Name + "_mesh";
                    newNode.Mesh = model.LogicalMeshes[^1];
                    newNode.Skin = skin;
                }
                else
                {
                    model.LogicalNodes[n].LocalTransform = AffineTransform.Identity;
                    model.LogicalNodes[n].Mesh = model.LogicalMeshes[^1];
                    model.LogicalNodes[n].Skin = skin;
                }
            }
        }

        /*if (isSkinned & skin != null)
        {
            BinaryWriterEx bw = new BinaryWriterEx(false);

            List<Vector3>[] verticesPerJoint = new List<Vector3>[skin.Joints.Count];
            for (int i = 0; i < skin.Joints.Count; i++)
            {
                verticesPerJoint[i] = new List<Vector3>();
            }
            foreach (MeshPrimitive primitive in model.LogicalMeshes.SelectMany(x => x.Primitives))
            {
                IList<Vector3> vertices = primitive.VertexAccessors["POSITION"].AsVector3Array();
                IList<Vector4> boneIDs = primitive.VertexAccessors["JOINTS_0"].AsVector4Array();
                IList<Vector4> boneWeights = primitive.VertexAccessors["WEIGHTS_0"].AsVector4Array();

                for (int v = 0; v < vertices.Count; v++)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (boneWeights[v][i] != 0)
                        {
                            verticesPerJoint[(int)boneIDs[v][i]].Add(vertices[v]);
                        }
                    }
                }
            }

            for (int i = 0; i < skin.Joints.Count; i++)
            {
                Node joint = skin.Joints[i];

                int vertexCount = verticesPerJoint[i].Count;
                float avgX = 0;
                float avgY = 0;
                float avgZ = 0;
                for (int v = 0; v < vertexCount; v++)
                {
                    avgX += verticesPerJoint[i][v].X;
                    avgY += verticesPerJoint[i][v].Y;
                    avgZ += verticesPerJoint[i][v].Z;
                }

                Matrix4x4 modelMatrix = Matrix4x4.Identity;
                
                if (vertexCount > 0)
                {
                    avgX /= vertexCount;
                    avgY /= vertexCount;
                    avgZ /= vertexCount;
                    
                    modelMatrix = Matrix4x4.CreateTranslation(avgX, avgY, avgZ);
                }
                
                //joint.LocalMatrix = Matrix4x4.Identity;
                Matrix4x4 bindTransform = GetBindMatrix(joint);
                Matrix4x4.Invert(bindTransform, out Matrix4x4 inverseBindMatrix);
                Matrix4x4 m = modelMatrix * inverseBindMatrix;
                bw.WriteSingle(m.M11);
                bw.WriteSingle(m.M12);
                bw.WriteSingle(m.M13);
                bw.WriteSingle(0);
                bw.WriteSingle(m.M21);
                bw.WriteSingle(m.M22);
                bw.WriteSingle(m.M23);
                bw.WriteSingle(0);
                bw.WriteSingle(m.M31);
                bw.WriteSingle(m.M32);
                bw.WriteSingle(m.M33);
                bw.WriteSingle(0);
                bw.WriteSingle(m.M41);
                bw.WriteSingle(m.M42);
                bw.WriteSingle(m.M43);
                bw.WriteSingle(1);
            }

            byte[] bufferContents = bw.FinishBytes();
            
            SharpGLTF.Schema2.Buffer inverseBindMatricesBuffer = model.CreateBuffer(bufferContents.Length);

            for (int i = 0; i < bufferContents.Length; i++)
            { 
                inverseBindMatricesBuffer.Content[i] = bufferContents[i];
            }
        
            BufferView inverseBindMatricesBufferView = model.UseBufferView(inverseBindMatricesBuffer, 0,
                bufferContents.Length);
            
            Accessor inverseBindMatricesAccessor = skin.UseInverseBindMatricesAccessor();
            inverseBindMatricesAccessor.SetData(inverseBindMatricesBufferView, 0, skin.Joints.Count, 
                DimensionType.MAT4, EncodingType.FLOAT, false);
        }*/
        
        string cwd = AppDomain.CurrentDomain.BaseDirectory;
        if (binaryOutput)
        {
            if (!Directory.Exists(cwd + "Output")) Directory.CreateDirectory(cwd + "Output");
            model.Save(cwd + $"Output/{fileName}.glb", new WriteSettings());
        }
        else
        {
            string outputFolder = cwd + $"Output/{fileName}";
            if (!Directory.Exists(outputFolder)) DirectoryUtils.CreateAllDirectories(outputFolder);
            model.Save($"{outputFolder}/{fileName}.gltf", new WriteSettings()
            {
                MergeBuffers = consolidateBuffers,
                JsonIndented = true
            });
        }
    }

    private static Matrix4x4 GetBindMatrix(Node joint)
    {
        if (joint.VisualParent != null)
        {
            Matrix4x4 parentMatrix = GetBindMatrix(joint.VisualParent);
            return joint.LocalMatrix * parentMatrix;
        }
        
        return joint.LocalMatrix;
    }

    private static void CreateGLTFMeshFromFlverMesh(this ModelRoot model, FLVER2 flver, List<FLVER2.Mesh> flvMeshes,
        Matrix4x4 masterBoneRotation, FLVER2MaterialInfoBank matInfoBank, List<short> jointIndices, bool isSkinned)
    {
        Mesh glMesh = model.CreateMesh(flver.Nodes[flvMeshes[0].NodeIndex].Name);
        
        foreach (FLVER2.Mesh flvMesh in flvMeshes)
        {
            FLVER2.FaceSet highestPolyFaceSet = flvMesh.FaceSets.OrderByDescending(x => x.Indices.Count).First();
            
            GetMeshVertexData(flver, flvMesh, isSkinned, out VertexDataFlags vertexDataFlags, out int vertexSize, 
                out int colorCount, out int uvCount);
        
            BinaryWriterEx bw = new (false);
            
            foreach (FLVER.Vertex v in flvMesh.Vertices)
            {
                if (vertexDataFlags.HasFlag(VertexDataFlags.Position))
                {
                    Vector3 positionVector = new Vector3(v.Position.X, v.Position.Y, v.Position.Z * -1);
                    //for (int i = 0; i < 4; i++)
                    //{
                    //    if (v.BoneWeights[i] != 0)
                    //    {
                            //Node jointNode = model.LogicalSkins[0].Joints[v.BoneIndices[i]];
                            //positionVector.X += positionVector.X - jointNode.LocalTransform.Translation.X;
                            //positionVector.Y += positionVector.Y - jointNode.LocalTransform.Translation.Y;
                            //positionVector.Z += positionVector.Z - jointNode.LocalTransform.Translation.Z;
                    //        Matrix4x4 bindMatrix = GetBindMatrix(jointNode);
                            //positionVector = Vector3.Transform(positionVector, bindMatrix);
                    //    }
                    //}
                    bw.WriteVector3(Vector3.Transform(positionVector, masterBoneRotation));
                    //bw.WriteVector3(positionVector);
                }

                if (vertexDataFlags.HasFlag(VertexDataFlags.Normal))
                {
                    Vector3 reversedNormal = new Vector3(v.Normal.X, v.Normal.Y, v.Normal.Z * -1);
                    reversedNormal = Vector3.Transform(reversedNormal, masterBoneRotation);
                    bw.WriteVector3(Vector3.Normalize(reversedNormal));
                }

                if (vertexDataFlags.HasFlag(VertexDataFlags.Tangent))
                {
                    Vector3 tangentXYZ = new Vector3(v.Tangents[0].X, v.Tangents[0].Y, v.Tangents[0].Z * -1);
                    tangentXYZ = Vector3.Transform(tangentXYZ, masterBoneRotation);
                    bw.WriteVector3(Vector3.Normalize(tangentXYZ));
                    bw.WriteSingle(v.Tangents[0].W);
                }

                for (int i = 0; i < colorCount; i++)
                {
                    bw.WriteVector4(new Vector4(v.Colors[i].R, v.Colors[i].G, v.Colors[i].B, v.Colors[i].A));
                }

                for (int i = 0; i < uvCount; i++)
                {
                    bw.WriteVector2(new Vector2(v.UVs[i].X, v.UVs[i].Y));
                }

                if (vertexDataFlags.HasFlag(VertexDataFlags.Joints))
                {
                    if (v.BoneIndices[0] == 0)
                    {
                        bw.WriteUInt16(0);
                    }
                    else
                    {
                        bw.WriteUInt16((ushort)jointIndices.IndexOf((short)v.BoneIndices[0]));
                    }
                    
                    if (v.BoneIndices[1] == 0)
                    {
                        bw.WriteUInt16(0);
                    }
                    else
                    {
                        bw.WriteUInt16((ushort)jointIndices.IndexOf((short)v.BoneIndices[1]));
                    }
                    
                    if (v.BoneIndices[2] == 0)
                    {
                        bw.WriteUInt16(0);
                    }
                    else
                    {
                        bw.WriteUInt16((ushort)jointIndices.IndexOf((short)v.BoneIndices[2]));
                    }
                    
                    if (v.BoneIndices[3] == 0)
                    {
                        bw.WriteUInt16(0);
                    }
                    else
                    {
                        bw.WriteUInt16((ushort)jointIndices.IndexOf((short)v.BoneIndices[3]));
                    }

                    //float correctBoneWeight0 = v.BoneIndices[0] == 0 ? 0 : v.BoneWeights[0];
                    //float correctBoneWeight1 = v.BoneIndices[1] == 0 ? 0 : v.BoneWeights[1];
                    //float correctBoneWeight2 = v.BoneIndices[2] == 0 ? 0 : v.BoneWeights[2];
                    //float correctBoneWeight3 = v.BoneIndices[3] == 0 ? 0 : v.BoneWeights[3];

                    float sum = v.BoneWeights[0] + v.BoneWeights[1] + v.BoneWeights[2] + v.BoneWeights[3];


                    Vector4 fixedWeights = new Vector4(
                        v.BoneWeights[0] / sum,
                        v.BoneWeights[1] / sum,
                        v.BoneWeights[2] / sum,
                        v.BoneWeights[3] / sum);

                    bw.WriteVector4(fixedWeights);
                }

                /*if (vertexDataFlags.HasFlag(VertexDataFlags.SingleJoint))
                {
                    bw.WriteByte((byte)v.NormalW);
                    bw.WriteByte(0);
                    bw.WriteByte(0);
                    bw.WriteByte(0);

                    bw.WriteVector4(new Vector4(1, 0, 0, 0));
                }*/
            }

            bool shortIndices = flvMesh.Vertices.Count < ushort.MaxValue;
                
            foreach (uint index in highestPolyFaceSet.Indices)
            {
                if (shortIndices)
                {
                    bw.WriteUInt16((ushort)index);
                }
                else
                {
                    bw.WriteUInt32(index);
                }
            }
            
            byte[] bufferContents = bw.FinishBytes();
            
            SharpGLTF.Schema2.Buffer meshBuffer = model.CreateBuffer(bufferContents.Length);

            for (int i = 0; i < bufferContents.Length; i++)
            {
                meshBuffer.Content[i] = bufferContents[i];
            }
            
            BufferView vertexBufferView = model.UseBufferView(meshBuffer, 0,
                vertexSize * flvMesh.Vertices.Count, vertexSize, BufferMode.ARRAY_BUFFER);
            
            int indicesBufferSize = highestPolyFaceSet.Indices.Count * (shortIndices ? 2 : 4);
        
            BufferView indicesBufferView = model.UseBufferView(meshBuffer, vertexSize * flvMesh.Vertices.Count, indicesBufferSize, 0,
                BufferMode.ELEMENT_ARRAY_BUFFER);
            
            MeshPrimitive primitive = glMesh.CreatePrimitive();
            primitive.DrawPrimitiveType = highestPolyFaceSet.TriangleStrip ? SharpGLTF.Schema2.PrimitiveType.TRIANGLE_STRIP : SharpGLTF.Schema2.PrimitiveType.TRIANGLES;
            Material material = model.CreateMaterial(flver.Materials[flvMesh.MaterialIndex].Name);
            primitive.Material = material;
            material.InitializePBRSpecularGlossiness();
            
            FLVER2.Material flvMaterial = flver.Materials[flvMesh.MaterialIndex];
            string mtdFileName = Path.GetFileName(flvMaterial.MTD);
            
            material.Extras = new JsonObject()
            {
                { "MTD", mtdFileName }
            };
            
            /*matInfoBank.MaterialDefs.TryGetValue(mtdFileName.ToLower(), out FLVER2MaterialInfoBank.MaterialDef? materialDef);
            if (materialDef != null)
            {
                List<FLVER2MaterialInfoBank.FlverTextureChannelType> channelTypes = new List<FLVER2MaterialInfoBank.FlverTextureChannelType>();
                for (int i = 0; i < flvMaterial.Textures.Count; i++)
                {
                    FLVER2.Texture flvTexture = flvMaterial.Textures[i];
                    string texName = Path.GetFileNameWithoutExtension(flvTexture.Path) + ".png";
                    model.UseImageWithFile($"../{texName}");

                    if (channelTypes[i].Semantic == TextureChannelSemantic.Diffuse)
                    {
                        MaterialChannel? diffuseChannel = material.FindChannel("Diffuse");
                        if (diffuseChannel != null)
                        {
                            //diffuseChannel.Value.SetTexture()
                        }
                    }
                }
            }*/

            Accessor indexAccessor = model.CreateAccessor();
            indexAccessor.SetIndexData(indicesBufferView, 0,
                highestPolyFaceSet.Indices.Count, shortIndices ? IndexEncodingType.UNSIGNED_SHORT : IndexEncodingType.UNSIGNED_INT);
            primitive.IndexAccessor = indexAccessor;
            
            int bufferOffset = 0;
        
            if (vertexDataFlags.HasFlag(VertexDataFlags.Position))
            {
                Accessor positionAccessor = model.CreateAccessor();
                positionAccessor.SetVertexData(vertexBufferView, bufferOffset, flvMesh.Vertices.Count);
                primitive.SetVertexAccessor("POSITION", positionAccessor);

                bufferOffset += 12;
            }
            
            if (vertexDataFlags.HasFlag(VertexDataFlags.Normal))
            {
                Accessor normalAccessor = model.CreateAccessor();
                normalAccessor.SetVertexData(vertexBufferView, bufferOffset, flvMesh.Vertices.Count);
                primitive.SetVertexAccessor("NORMAL", normalAccessor);

                bufferOffset += 12;
            }
            
            if (vertexDataFlags.HasFlag(VertexDataFlags.Tangent))
            {
                Accessor tangentAccessor = model.CreateAccessor();
                tangentAccessor.SetVertexData(vertexBufferView, bufferOffset, flvMesh.Vertices.Count,
                    DimensionType.VEC4);
                primitive.SetVertexAccessor("TANGENT", tangentAccessor);
                
                bufferOffset += 16;
            }

            for (int i = 0; i < colorCount; i++)
            {
                Accessor colorAccessor = model.CreateAccessor();
                colorAccessor.SetVertexData(vertexBufferView, bufferOffset, flvMesh.Vertices.Count, DimensionType.VEC4);
                primitive.SetVertexAccessor("COLOR_" + i, colorAccessor);
                
                bufferOffset += 16;
            }

            for (int i = 0; i < uvCount; i++)
            {
                Accessor uvAccessor = model.CreateAccessor();
                uvAccessor.SetVertexData(vertexBufferView, bufferOffset, flvMesh.Vertices.Count, DimensionType.VEC2);
                primitive.SetVertexAccessor("TEXCOORD_" + i, uvAccessor);
                
                bufferOffset += 8;
            }
            
            if (vertexDataFlags.HasFlag(VertexDataFlags.Joints) || vertexDataFlags.HasFlag(VertexDataFlags.SingleJoint))
            {
                Accessor boneIndicesAccessor = model.CreateAccessor();
                boneIndicesAccessor.SetVertexData(vertexBufferView, bufferOffset, flvMesh.Vertices.Count,
                DimensionType.VEC4, EncodingType.UNSIGNED_SHORT);
                primitive.SetVertexAccessor("JOINTS_0", boneIndicesAccessor);

                bufferOffset += 8;
                
                Accessor boneWeightsAccessor = model.CreateAccessor();
                boneWeightsAccessor.SetVertexData(vertexBufferView, bufferOffset, flvMesh.Vertices.Count,
                    DimensionType.VEC4);
                primitive.SetVertexAccessor("WEIGHTS_0", boneWeightsAccessor);

                bufferOffset += 16;
            }
        }
    }

    private static void GetMeshVertexData(FLVER2 flver, FLVER2.Mesh flvMesh, bool isSkinned,
        out VertexDataFlags flags, out int vertexSize, out int colorCount, out int uvCount)
    {
        flags = VertexDataFlags.Empty;
        vertexSize = 0;

        colorCount = 0;
        uvCount = 0;
        foreach (FLVER2.VertexBuffer vBuffer in flvMesh.VertexBuffers)
        {
            foreach (FLVER.LayoutMember layout in flver.BufferLayouts[vBuffer.LayoutIndex])
            {
                switch (layout.Semantic)
                {
                    case FLVER.LayoutSemantic.Position:
                        if (flags.HasFlag(VertexDataFlags.Position)) break;
                        flags |= VertexDataFlags.Position;
                        vertexSize += 12;
                        break;
                    case FLVER.LayoutSemantic.Normal:
                        if (flags.HasFlag(VertexDataFlags.Normal)) break;
                        flags |= VertexDataFlags.Normal;
                        vertexSize += 12;
                        break;
                    case FLVER.LayoutSemantic.Tangent:
                        if (flags.HasFlag(VertexDataFlags.Tangent)) break;
                        flags |= VertexDataFlags.Tangent;
                        vertexSize += 16;
                        break;
                    case FLVER.LayoutSemantic.VertexColor:
                        switch (colorCount)
                        {
                            case 0:
                                flags |= VertexDataFlags.Color0;
                                vertexSize += 16;
                                colorCount++;
                                break;
                            case 1:
                                flags |= VertexDataFlags.Color1;
                                vertexSize += 16;
                                colorCount++;
                                break;
                        }
                        break;
                    case FLVER.LayoutSemantic.UV:
                        int numUVs = layout.Size / 4;
                        for (int i = 0; i < numUVs; i++)
                        {
                            switch (uvCount)
                            {
                                case 0:
                                    flags |= VertexDataFlags.TexCoord0;
                                    vertexSize += 8;
                                    uvCount++;
                                    break;
                                case 1:
                                    flags |= VertexDataFlags.TexCoord1;
                                    vertexSize += 8;
                                    uvCount++;
                                    break;
                                case 2:
                                    flags |= VertexDataFlags.TexCoord2;
                                    vertexSize += 8;
                                    uvCount++;
                                    break;
                                case 3:
                                    flags |= VertexDataFlags.TexCoord3;
                                    vertexSize += 8;
                                    uvCount++;
                                    break;
                                case 4:
                                    flags |= VertexDataFlags.TexCoord4;
                                    vertexSize += 8;
                                    uvCount++;
                                    break;
                                case 5:
                                    flags |= VertexDataFlags.TexCoord5;
                                    vertexSize += 8;
                                    uvCount++;
                                    break;
                            }
                        }
                        break;
                    case FLVER.LayoutSemantic.BoneIndices:
                        if (!isSkinned) break;
                        if (flags.HasFlag(VertexDataFlags.Joints)) break;
                        flags |= VertexDataFlags.Joints;
                        vertexSize += 24; // 8 for the indices, 16 for the weights
                        break;
                }
            }
        }

        if (flvMesh.UseBoneWeights == false)
        {
            flags |= VertexDataFlags.SingleJoint;
            vertexSize += 24; // 8 for the indices, 16 for the weights
        }
    }

    private static AffineTransform GetTransformationMatrix(Vector3 translation, Vector3 rotation, Vector3 scale)
    {
        Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(new Vector3(translation.X, translation.Y, translation.Z * -1));
        Matrix4x4 rotationMatrix =  Matrix4x4.CreateRotationX(rotation.X * -1) * Matrix4x4.CreateRotationZ(rotation.Z) * Matrix4x4.CreateRotationY(rotation.Y * -1);
        Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(scale);
        
        //return new AffineTransform(scale, Quaternion.CreateFromRotationMatrix(rotationMatrix), new Vector3(translation.X, translation.Y, translation.Z * -1));
        return new AffineTransform(translationMatrix * rotationMatrix * scaleMatrix);
    }
    
    public static AffineTransform GetTransformationMatrix(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        return new AffineTransform(scale, rotation, new Vector3(translation.X, translation.Y, translation.Z * -1));
    }
    
    public static Quaternion GetQuaternionFromEuler(Vector3 rotation)
    {
        Matrix4x4 rotationMatrix =  Matrix4x4.CreateRotationX(rotation.X * -1) * Matrix4x4.CreateRotationZ(rotation.Z) * Matrix4x4.CreateRotationY(rotation.Y * -1);
        return Quaternion.CreateFromRotationMatrix(rotationMatrix);
    }
    
    public static void AddNodeToHierarchy(FLVER2 flver, short childIndex, Node parentGlNode, Dictionary<short, short> flverNodeToGlNodeMap, hkaSkeleton? skeleton, List<short> jointIndices)
    {
        FLVER.Node flvNode = flver.Nodes[childIndex];
        
        Node glNode = parentGlNode.CreateNode(flvNode.Name);
        
        
        flverNodeToGlNodeMap.Add(childIndex, (short)glNode.LogicalIndex);
        
        if (skeleton != null)
        {
            jointIndices.Add(childIndex);

            hkaSkeleton.BonePose bonePose = skeleton.ReferencePose[jointIndices.Count - 1];
            Vector3 translation = new Vector3(bonePose.Translation.X, bonePose.Translation.Y, bonePose.Translation.Z * -1);
            Quaternion rotation = new Quaternion(bonePose.Rotation.X * -1, bonePose.Rotation.Y * -1, bonePose.Rotation.Z, bonePose.Rotation.W);
            Vector3 scale = new Vector3(bonePose.Scale.X, bonePose.Scale.Y, bonePose.Scale.Z);
            
            glNode.LocalTransform = new AffineTransform(scale, rotation, translation);

            //Quaternion quatRotation = GetQuaternionFromEuler(flvNode.Rotation);
            
            //glNode.LocalTransform = GetTransformationMatrix(flvNode.Translation, flvNode.Rotation, flvNode.Scale);
        }
        else
        {
            glNode.LocalTransform = GetTransformationMatrix(flvNode.Translation, flvNode.Rotation, flvNode.Scale);
        }

        if (flvNode.FirstChildIndex > -1)
        {
            AddNodeToHierarchy(flver, flvNode.FirstChildIndex, glNode, flverNodeToGlNodeMap, skeleton, jointIndices);
        }
        
        if (flvNode.NextSiblingIndex > -1)
        {
            AddNodeToHierarchy(flver, flvNode.NextSiblingIndex, parentGlNode, flverNodeToGlNodeMap, skeleton, jointIndices);
        }
    }
}