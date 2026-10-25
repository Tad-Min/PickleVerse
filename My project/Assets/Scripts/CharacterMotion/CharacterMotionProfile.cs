using System;
using UnityEngine;

namespace PickleVerse.CharacterMotion
{
    public enum SecondaryPart { Ponytail, ShortHair, Skirt }
    [CreateAssetMenu(menuName = "PickleVerse/Character secondary motion")]
    public sealed class CharacterMotionProfile : ScriptableObject
    {
        [Serializable]
        public class Joint
        {
            public string name, anchor;
            public int parent = -1;
            public Vector3 localPosition, endOffset;
            public SecondaryPart part;
            public float stiffness = 200, damping = 10, maximumAngle = 20, gravityScale = .1f;
            public int blendShapeX = -1, blendShapeZ = -1;
            public Vector3 meshPivot, meshEnd;
        }
        public Mesh mesh;
        public Mesh foreheadMesh;
        public Material foreheadMaterial;
        public int originalVertexCount, originalBoneCount;
        public string[] originalBoneNames;
        public Joint[] joints;
    }
}
