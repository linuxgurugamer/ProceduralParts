﻿using KSPAPIExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ProceduralParts
{
    class ProceduralShapeOctagon : ProceduralAbstractShape
    {
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Diameter", guiFormat = "F3", guiUnits = "m"),
         UI_FloatEdit(scene = UI_Scene.Editor, incrementSlide = SliderPrecision, sigFigs = 5, unit = "m", useSI = true)]
        public float Diameter = 1f;
        private float oldDiameter;

        [KSPField]
        public string TopNodeName = "top";

        [KSPField]
        public string BottomNodeName = "bottom";

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Length", guiFormat = "F3", guiUnits = "m"),
         UI_FloatEdit(scene = UI_Scene.Editor, incrementSlide = SliderPrecision, sigFigs = 5, unit = "m", useSI = true)]
        public float Length = 1f;
        private float oldLength;
        private static readonly float NormSideLength = 1 / (1 + Mathf.Sqrt(2));
        private static readonly float NormHalfSideLength = NormSideLength / 2;
        private static readonly float NormSideOffset = 0.5f - NormHalfSideLength;
        private static readonly float NormRadius = 0.5f;
        private const float NormHalfHeight = 0.5f;
        private float HalfSideLength { get => NormHalfSideLength * Diameter; }
        private float Radius { get => NormRadius * Diameter; }
        private float HalfHeight { get => NormHalfHeight * Length; }
        private float SideOffset { get => NormSideOffset * Diameter; }
        private float Area { get => Diameter * Diameter - SideOffset * SideOffset; }
        private float VolumeCalculated { get => Area * Length; }

        private const int CapVerticesPerCap = 8;
        private const int SideVerticesPerCap = 16;
        private const int SideTriangles = 16;
        private const int TrianglesPerCap = 6;
        private static float InvSqrt2 = 1 / Mathf.Sqrt(2);
        private static readonly Vector3[] Normals = {
            new Vector3(-InvSqrt2, 0, -InvSqrt2),
            new Vector3(0, 0, -1),
            new Vector3(InvSqrt2, 0, -InvSqrt2),
            new Vector3(1, 0, 0),
            new Vector3(InvSqrt2, 0, InvSqrt2),
            new Vector3(0, 0, 1),
            new Vector3(-InvSqrt2, 0, InvSqrt2),
            new Vector3(-1, 0, 0)
        };
        

        public override void OnStart(StartState state)
        {
            UpdateTechConstraints();
        }

        public override Vector3 FromCylindricCoordinates(ShapeCoordinates coords)
        {
            Vector3 position = new Vector3();

            switch (coords.HeightMode)
            {
                case ShapeCoordinates.YMode.RELATIVE_TO_SHAPE:
                    position.y = HalfHeight * coords.y;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_CENTER:
                    position.y = coords.y;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_BOTTOM:
                    position.y = coords.y - HalfHeight;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_TOP:
                    position.y = coords.y + HalfHeight;
                    break;
                default:
                    Debug.LogError("Can not handle PartCoordinate attribute: " + coords.HeightMode);
                    position.y = 0.0f;
                    break;
            }

            var radius = coords.r;

            if (coords.RadiusMode != ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_CENTER)
            {
                radius = coords.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_RADIUS ? Radius + coords.r : Radius * coords.r;
            }

            var theta = Mathf.Lerp(0, Mathf.PI * 2f, coords.u);

            position.x = Mathf.Cos(theta) * radius;
            position.z = -Mathf.Sin(theta) * radius;

            Log("FromCylindricCoordinates called: " + coords + ", returned pos: " + position);

            return position;
        }

        private static void Log(string message)
        {
            Debug.Log("[PP] " + message);
        }

        public override void GetCylindricCoordinates(Vector3 position, ShapeCoordinates shapeCoordinates)
        {
            Log("GetCylindricCoordinates called: pos:" + position + ", coords:" + shapeCoordinates);
            Vector2 direction = new Vector2(position.x, position.z);

            switch (shapeCoordinates.HeightMode)
            {
                case ShapeCoordinates.YMode.RELATIVE_TO_SHAPE:
                    shapeCoordinates.y = position.y / HalfHeight;
                    if (float.IsNaN(shapeCoordinates.y))
                        shapeCoordinates.y = 0f;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_CENTER:
                    shapeCoordinates.y = position.y;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_BOTTOM:
                    shapeCoordinates.y = position.y - -HalfHeight;
                    break;

                case ShapeCoordinates.YMode.OFFSET_FROM_SHAPE_TOP:
                    shapeCoordinates.y = position.y - HalfHeight;
                    break;
                default:
                    Debug.LogError("Can not handle PartCoordinate attribute: " + shapeCoordinates.HeightMode);
                    shapeCoordinates.y = 0.0f;
                    break;
            }


            shapeCoordinates.r = 0;

            float theta = Mathf.Atan2(-direction.y, direction.x);

            shapeCoordinates.u = (Mathf.InverseLerp(-Mathf.PI, Mathf.PI, theta) + 0.5f) % 1.0f;
            if (float.IsNaN(shapeCoordinates.u))
                shapeCoordinates.u = 0f;

            if (shapeCoordinates.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_CENTER)
            {
                shapeCoordinates.r = direction.magnitude;
                return;
            }

            shapeCoordinates.r = shapeCoordinates.RadiusMode == ShapeCoordinates.RMode.OFFSET_FROM_SHAPE_RADIUS ?
                direction.magnitude - Radius :
                direction.magnitude / Radius; // RELATIVE_TO_SHAPE_RADIUS

            // sometimes, if the shapes radius is 0, r rersults in NaN
            if (float.IsNaN(shapeCoordinates.r) || float.IsPositiveInfinity(shapeCoordinates.r) || float.IsNegativeInfinity(shapeCoordinates.r))
            {
                shapeCoordinates.r = 0;
            }
        }

        public override void UpdateTechConstraints()
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;

            if (PPart.lengthMin == PPart.lengthMax)
                Fields["Length"].guiActiveEditor = false;
            else
            {
                UI_FloatEdit lengthEdit = (UI_FloatEdit)Fields["Length"].uiControlEditor;
                lengthEdit.maxValue = PPart.lengthMax;
                lengthEdit.minValue = PPart.lengthMin;
                lengthEdit.incrementLarge = PPart.lengthLargeStep;
                lengthEdit.incrementSmall = PPart.lengthSmallStep;
                Length = Mathf.Clamp(Length, PPart.lengthMin, PPart.lengthMax);
            }

            if (PPart.diameterMin == PPart.diameterMax)
                Fields["Diameter"].guiActiveEditor = false;
            else
            {
                UI_FloatEdit diameterEdit = (UI_FloatEdit)Fields["Diameter"].uiControlEditor;
                if (null != diameterEdit)
                {
                    diameterEdit.maxValue = PPart.diameterMax;
                    diameterEdit.minValue = PPart.diameterMin;
                    diameterEdit.incrementLarge = PPart.diameterLargeStep;
                    diameterEdit.incrementSmall = PPart.diameterSmallStep;
                    Diameter = Mathf.Clamp(Diameter, PPart.diameterMin, PPart.diameterMax);
                }
                else
                    Debug.LogError("*PP* could not find field 'diameter'");
            }
        }

        public override void UpdateTFInterops()
        {
        }

        protected override void UpdateShape(bool force)
        {
            if (!force && Diameter == oldDiameter && Length == oldLength)
            {
                return;
            }
            Debug.Log($"UpdateShape called: {force}, dia: {Diameter}, oldDia: {oldDiameter}, length: {Length}, oldLength: {oldLength}");

            RecalculateVolume();

            UpdateNodeSize(TopNodeName);
            UpdateNodeSize(BottomNodeName);
            MoveAttachments();
            GenerateSideMesh();
            GenerateCapMesh();
            GenerateColliderMesh();

            UpdateProps();
            oldLength = Length;
            oldDiameter = Diameter;
            RaiseModelAndColliderChanged();
        }

        private void RecalculateVolume()
        {
            var volume = VolumeCalculated;

            if (HighLogic.LoadedSceneIsEditor)
            {
                volume = ClampToVolumeRestrictions(volume);
            }
            Volume = volume;
        }

        private float ClampToVolumeRestrictions(float volume)
        {
            var oldVolume = volume;
            volume = Mathf.Clamp(volume, PPart.volumeMin, PPart.volumeMax);
            if (volume != oldVolume)
            {
                var excessVol = oldVolume - volume;
                if (oldDiameter != Diameter)
                {
                    var requiredDiameter = Mathf.Sqrt(volume / Length / (1 - NormSideOffset * NormSideOffset));
                    Diameter = TruncateForSlider(requiredDiameter, -excessVol);
                }
                else
                {
                    var requiredLength = volume / Area;
                    Length = TruncateForSlider(requiredLength, -excessVol);
                }
                volume = VolumeCalculated;
                RefreshPartEditorWindow();
            }

            return volume;
        }

        private void UpdateProps()
        {
            foreach (PartModule pm in GetComponents<PartModule>())
            {
                IProp prop = pm as IProp;
                if (null != prop)
                    prop.UpdateProp();
            }
        }

        private void GenerateColliderMesh()
        {
            var mesh = new UncheckedMesh(CapVerticesPerCap * 2, SideTriangles);
            GenerateCapVertices(mesh, -HalfHeight, 0, false);
            GenerateCapVertices(mesh, HalfHeight, CapVerticesPerCap, true);
            GenerateSideTriangles(mesh, CapVerticesPerCap, 1);

            var colliderMesh = new Mesh();
            mesh.WriteTo(colliderMesh);
            PPart.ColliderMesh = colliderMesh;
        }

        private void GenerateCapMesh()
        {
            var mesh = new UncheckedMesh(CapVerticesPerCap * 2, TrianglesPerCap * 2);
            GenerateCapVertices(mesh, HalfHeight, 0, true);
            GenerateCapVertices(mesh, -HalfHeight, CapVerticesPerCap, false);
            GenerateCapTriangles(mesh, true);
            GenerateCapTriangles(mesh, false);

            WriteToAppropriateMesh(mesh, PPart.EndsIconMesh, EndsMesh);
        }

        private void GenerateSideMesh()
        {
            var mesh = new UncheckedMesh(SideVerticesPerCap * 2, SideTriangles);
            GenerateSideVertices(mesh, -HalfHeight, 0, 0);
            GenerateSideVertices(mesh, HalfHeight, 1, SideVerticesPerCap);
            GenerateSideTriangles(mesh, SideVerticesPerCap, 2);

            var tankULength = 8 * NormSideLength * Diameter * 2;
            var tankVLength = Length;

            //print("ULength=" + tankULength + " VLength=" + tankVLength);

            // set the texture scale.
            RaiseChangeTextureScale("sides", PPart.SidesMaterial, new Vector2(tankULength, tankVLength));
            WriteToAppropriateMesh(mesh, PPart.SidesIconMesh, SidesMesh);
        }

        private void UpdateNodeSize(string nodeName)
        {
            AttachNode node = part.attachNodes.Find(n => n.id == nodeName);
            if (node == null)
                return;
            node.size = Math.Min((int)(Diameter / PPart.diameterLargeStep), 3);

            // Breaking force and torque scales with the area of the surface (node size).
            node.breakingTorque = node.breakingForce = Mathf.Max(50 * node.size * node.size, 50);

            // Send messages for the changing of the ends
            RaiseChangeAttachNodeSize(node, Diameter, Mathf.PI * Diameter * Diameter * 0.25f);

            // TODO: separate out the meshes for each end so we can use the scale for texturing.
            RaiseChangeTextureScale(nodeName, PPart.EndsMaterial, new Vector2(Diameter, Diameter));
        }

        private static void WriteToAppropriateMesh(UncheckedMesh mesh, Mesh iconMesh, Mesh normalMesh)
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING)
            {
                mesh.WriteTo(iconMesh);
            }
            else
            {
                mesh.WriteTo(normalMesh);
            }
        }

        private static void GenerateSideTriangles(UncheckedMesh mesh, int numberOfCapVertices, int verticesPerCorner)
        {
            for (int i = 0; i < 8; i++)
            {
                var baseVertex = i * verticesPerCorner + verticesPerCorner - 1;
                mesh.triangles[i * 6] = baseVertex;
                mesh.triangles[i * 6 + 1] = baseVertex + numberOfCapVertices;
                mesh.triangles[i * 6 + 2] = (baseVertex + 1) % numberOfCapVertices;

                mesh.triangles[i * 6 + 3] = (baseVertex + 1) % numberOfCapVertices;
                mesh.triangles[i * 6 + 4] = baseVertex + numberOfCapVertices;
                mesh.triangles[i * 6 + 5] = (baseVertex + 1) % numberOfCapVertices + numberOfCapVertices;
            }
        }

        private static void GenerateCapTriangles(UncheckedMesh mesh, bool up)
        {
            var triangleOffset = up ? 0 : TrianglesPerCap * 3;
            var vertexOffset = up ? 0 : CapVerticesPerCap;
            for (int i = 0; i < 6; i++)
            {
                mesh.triangles[i * 3 + triangleOffset] = i + vertexOffset;
                mesh.triangles[i * 3 + 1 + triangleOffset] = (up ? 7 : i + 1) + vertexOffset;
                mesh.triangles[i * 3 + 2 + triangleOffset] = (up ? i + 1 : 7) + vertexOffset;
            }
        }

        private void GenerateSideVertices(UncheckedMesh mesh, float y, float v, int offset)
        {
            GenerateOctagonVertices(mesh, y, offset, 2);

            for (int i = 0; i < SideVerticesPerCap; i++)
            {
                mesh.uv[offset + (i + 1) % SideVerticesPerCap] = new Vector2((float)((i + 1) / 2) / CapVerticesPerCap, v);

                var normal = Normals[(i + 1) / 2 % Normals.Length];
                mesh.normals[offset + i] = normal;
                mesh.tangents[offset + i] = new Vector4(normal.z, 0, -normal.x, 1f);
            }
        }

        private void GenerateCapVertices(UncheckedMesh mesh, float y, int offset, bool up)
        {
            mesh.uv[offset + 0] = new Vector2(-NormHalfSideLength + 0.5f, 0);
            mesh.uv[offset + 1] = new Vector2(NormHalfSideLength + 0.5f, 0);
            mesh.uv[offset + 2] = new Vector2(1, -NormHalfSideLength + 0.5f);
            mesh.uv[offset + 3] = new Vector2(1, NormHalfSideLength + 0.5f);
            mesh.uv[offset + 4] = new Vector2(NormHalfSideLength + 0.5f, 1);
            mesh.uv[offset + 5] = new Vector2(-NormHalfSideLength + 0.5f, 1);
            mesh.uv[offset + 6] = new Vector2(0, NormHalfSideLength + 0.5f);
            mesh.uv[offset + 7] = new Vector2(0, -NormHalfSideLength + 0.5f);

            GenerateOctagonVertices(mesh, y, offset, 1);

            for (int i = 0; i < CapVerticesPerCap; i++)
            {
                var vertex = mesh.verticies[offset + i];
                mesh.normals[offset + i] = new Vector3(0, up ? 1 : -1, 0);
                mesh.tangents[offset + i] = new Vector4(-1, 0, 0, up ? 1 : -1);
            }
        }

        private void GenerateOctagonVertices(UncheckedMesh mesh, float y, int offset, int verticesPerCornerCount)
        {
            var vectors = new List<Vector3>()
            {
                new Vector3(-HalfSideLength, y, -Radius),
                new Vector3(HalfSideLength, y, -Radius),
                new Vector3(Radius, y, -HalfSideLength),
                new Vector3(Radius, y, HalfSideLength),
                new Vector3(HalfSideLength, y, Radius),
                new Vector3(-HalfSideLength, y, Radius),
                new Vector3(-Radius, y, HalfSideLength),
                new Vector3(-Radius, y, -HalfSideLength)
            };
            int i = 0;
            foreach(var vector in vectors)
            {
                for(int j = 0; j < verticesPerCornerCount; j++)
                {
                    mesh.verticies[offset + i] = vector;
                    i++;
                }
            }
        }

        public override object AddAttachment(TransformFollower transformFollower, bool normalized)
        {
            return normalized ? AddNormalizedAttachment(transformFollower) : AddNonNormalizedAttachment(transformFollower);
        }

        private enum Location
        {
            Top, Bottom, Side
        }

        private class Attachment
        {
            public TransformFollower follower;
            public Location location;
            public Vector2 uv;

            public LinkedListNode<Attachment> node;

            public override string ToString()
            {
                return "Attachment(location:" + location + ", uv=" + uv.ToString("F4") + ")";
            }
        }

        private readonly LinkedList<Attachment> topAttachments = new LinkedList<Attachment>();
        private readonly LinkedList<Attachment> bottomAttachments = new LinkedList<Attachment>();
        private readonly LinkedList<Attachment> sideAttachments = new LinkedList<Attachment>();

        private object AddNonNormalizedAttachment(TransformFollower transformFollower)
        {
            var position = transformFollower.transform.localPosition;

            var attachmentHeightToRadiusSquared = position.y * position.y / (position.x * position.x + position.z * position.z);
            var tankCornerHeightToRadiusSquared = Length * Length / (Diameter * Diameter);

            if(attachmentHeightToRadiusSquared > tankCornerHeightToRadiusSquared)
            {
                return AddNonNormalizedCapAttachment(transformFollower, position);
            }
            else
            {
                return AddNonNormalizedSideAttachment(transformFollower, position);
            }
        }

        private object AddNonNormalizedSideAttachment(TransformFollower transformFollower, Vector3 position)
        {
            var theta = Mathf.Atan2(-position.z, position.x);
            var uv = GetNonNormalizedSideAttachmentUv(position, theta);
            var orientation = SideAttachOrientation(theta, out Vector3 normal);
            var attachment = CreateAttachment(transformFollower, uv, Location.Side, orientation);

            Log("Adding non-normalized side attachment to position=" + position + " location=" + attachment.location + " uv=" + attachment.uv + " attach=" + transformFollower.name);
            AddSideAttachment(attachment);

            return attachment;
        }

        private Vector2 GetNonNormalizedSideAttachmentUv(Vector3 position, float theta)
        {
            var uv = new Vector2((Mathf.InverseLerp(-Mathf.PI, Mathf.PI, theta) + 0.5f) % 1.0f, position.y / Length + 0.5f);
            if (float.IsNaN(uv[0]))
            {
                uv[0] = 0f;
            }

            return uv;
        }

        private Attachment AddNonNormalizedCapAttachment(TransformFollower transformFollower, Vector3 position)
        {
            var uv = new Vector2(position.x / Radius + 0.5f, position.z / Radius + 0.5f);
            return AddCapAttachment(transformFollower, position, uv);
        }

        private Attachment AddCapAttachment(TransformFollower follower, Vector3 position, Vector2 uv)
        {
            Log("Adding normalized attachment to position=" + position + " attach=" + follower.name);
            if (position.y > 0)
            {
                return AddCapAttachment(follower, uv, Location.Top, Quaternion.LookRotation(Vector3.up, Vector3.right));
            }
            else
            {
                return AddCapAttachment(follower, uv, Location.Bottom, Quaternion.LookRotation(Vector3.down, Vector3.left));
            }
        }

        private Attachment AddCapAttachment(TransformFollower transformFollower, Vector2 uv, Location location, Quaternion orientation)
        {
            var attachment = CreateAttachment(transformFollower, uv, location, orientation);

            var attachmentList = location == Location.Top ? topAttachments : bottomAttachments;
            attachment.node = attachmentList.AddLast(attachment);
            Log("Adding non-normalized attachment to position= location=" + attachment.location + " uv=" + attachment.uv + " attach=" + transformFollower.name);

            return attachment;
        }

        private static Attachment CreateAttachment(TransformFollower transformFollower, Vector2 uv, Location location, Quaternion orientation)
        {
            transformFollower.SetLocalRotationReference(orientation);
            return new Attachment
            {
                uv = uv,
                follower = transformFollower,
                location = location,
            };
        }

        private object AddNormalizedAttachment(TransformFollower follower)
        {
            var position = follower.transform.localPosition;
            Attachment attachment;

            // as the position might be after some rotation and translation, it might not be exactly +/- 0.5
            if (Mathf.Abs(Mathf.Abs(position.y) - 0.5f) < 1e-5f)
            {
                attachment = AddNormalizedCapAttachment(follower, position);
            }
            else
            {
                attachment = AddNormalizedSideAttachment(follower, position);
            }
            ForceNextUpdate();
            return attachment;
        }

        private Attachment AddNormalizedSideAttachment(TransformFollower follower, Vector3 position)
        {
            var uv = GetNormalizedSideAttachmentUv(follower, position);
            var normalVector = new Vector3(position.x * 2f, 0, position.z * 2f);
            var attachment = CreateAttachment(follower, uv, Location.Side, Quaternion.FromToRotation(Vector3.up, normalVector));
            AddSideAttachment(attachment);
            return attachment;
        }

        private static Vector2 GetNormalizedSideAttachmentUv(TransformFollower follower, Vector3 position)
        {
            Log("Adding normalized side attachment to position=" + position + " attach=" + follower.name);
            var theta = Mathf.Atan2(-position.z, position.x);
            var uv = new Vector2(GetSideAttachmentU(theta), 0.5f - position.y);
            if (float.IsNaN(uv[0]))
            {
                uv[0] = 0f;
            }

            return uv;
        }

        private static float GetSideAttachmentU(float theta)
        {
            return (Mathf.InverseLerp(-Mathf.PI, Mathf.PI, theta) + 0.5f) % 1.0f;
        }

        private Attachment AddNormalizedCapAttachment(TransformFollower follower, Vector3 position)
        {
            var uv = new Vector2(position.x + 0.5f, position.z + 0.5f);
            return AddCapAttachment(follower, position, uv);
        }

        private void AddSideAttachment(Attachment attachment)
        {
            for (LinkedListNode<Attachment> node = sideAttachments.First; node != null; node = node.Next)
                if (node.Value.uv[1] > attachment.uv[1])
                {
                    attachment.node = sideAttachments.AddBefore(node, attachment);
                    return;
                }
            attachment.node = sideAttachments.AddLast(attachment);
        }

        public override TransformFollower RemoveAttachment(object data, bool normalize)
        {
            Log("Remove attachment called: norm: " + normalize);
            Attachment attach = (Attachment)data;
            switch (attach.location)
            {
                case Location.Top:
                    topAttachments.Remove(attach.node);
                    if (normalize)
                        attach.follower.transform.localPosition = new Vector3(attach.uv[0] - 0.5f, 0.5f, attach.uv[1] - 0.5f);
                    break;
                case Location.Bottom:
                    bottomAttachments.Remove(attach.node);
                    if (normalize)
                        attach.follower.transform.localPosition = new Vector3(attach.uv[0] - 0.5f, -0.5f, attach.uv[1] - 0.5f);
                    break;
                case Location.Side:
                    sideAttachments.Remove(attach.node);

                    if (normalize)
                    {
                        float theta = Mathf.Lerp(0, Mathf.PI * 2f, attach.uv[0]);
                        float x = Mathf.Cos(theta);
                        float z = -Mathf.Sin(theta);

                        Vector3 normal = new Vector3(x, 0, z);
                        attach.follower.transform.localPosition = new Vector3(normal.x * 0.5f, 0.5f - attach.uv[1], normal.z * 0.5f);
                        attach.follower.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
                    }
                    break;
            }

            if (normalize)
                attach.follower.ForceUpdate();
            return attach.follower;
        }

        private void MoveAttachments()
        {
            foreach (var attachment in topAttachments)
            {
                MoveCapAttachment(attachment, HalfHeight);
            }
            foreach (var attachment in bottomAttachments)
            {
                MoveCapAttachment(attachment, -HalfHeight);
            }
            foreach (var attachment in sideAttachments)
            {
                MoveSideAttachment(attachment);
            }
        }

        private void MoveSideAttachment(Attachment attachment)
        {
            var theta = Mathf.Lerp(0, Mathf.PI * 2f, attachment.uv[0]);

            var x = Mathf.Cos(theta) * Radius;
            var z = -Mathf.Sin(theta) * Radius;

            var pos = new Vector3(x, attachment.uv[1] - 0.5f, z);
            Log("Moving side attachment:" + attachment + " to:" + pos.ToString("F3"));
            attachment.follower.transform.localPosition = pos;

            var orientation = SideAttachOrientation(theta, out Vector3 normal);

            Log("Moving to orientation: normal: " + normal.ToString("F3") + " theta:" + (theta * 180f / Mathf.PI) + orientation.ToStringAngleAxis());

            attachment.follower.transform.localRotation = orientation;
            attachment.follower.ForceUpdate();
        }

        private static Quaternion SideAttachOrientation(float theta, out Vector3 normal)
        {
            normal = Quaternion.AngleAxis(theta * 180 / Mathf.PI, Vector3.up) * new Vector2(1, 0);
            return Quaternion.FromToRotation(Vector3.up, normal);
        }

        private void MoveCapAttachment(Attachment attachment, float yCoordinate)
        {
            var pos = new Vector3(
                                (attachment.uv[0] - 0.5f) * Radius,
                                yCoordinate,
                                (attachment.uv[1] - 0.5f) * Radius);
            Log("Moving cap attachment:" + attachment + " to:" + pos.ToString("F7") + " uv: " + attachment.uv.ToString("F5"));
            attachment.follower.transform.localPosition = pos;
            attachment.follower.ForceUpdate();
        }
    }
}