namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Avatar definition for virtual environment
    /// </summary>
    public class AvatarDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ModelPath { get; set; } = string.Empty;
        public Vector3 Position { get; set; } = new();
        public Vector3 Rotation { get; set; } = new();
        public Vector3 Scale { get; set; } = new(1, 1, 1);
        public string? AIContactId { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
    }

    /// <summary>
    /// 3D position and rotation
    /// </summary>
    public class Vector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3() { }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Avatar pose information
    /// </summary>
    public class Pose
    {
        public Vector3 Position { get; set; } = new();
        public Vector3 Rotation { get; set; } = new();
        public Dictionary<string, float> BoneRotations { get; set; } = new();
        public string? AnimationState { get; set; }
    }

    /// <summary>
    /// Virtual world object
    /// </summary>
    public class WorldObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Vector3 Position { get; set; } = new();
        public Vector3 Rotation { get; set; } = new();
        public Vector3 Scale { get; set; } = new(1, 1, 1);
        public Dictionary<string, object> Properties { get; set; } = new();
        public bool IsInteractive { get; set; }
    }

    /// <summary>
    /// Scene information
    /// </summary>
    public class SceneInfo
    {
        public string Name { get; set; } = string.Empty;
        public List<AvatarDefinition> Avatars { get; set; } = new();
        public List<WorldObject> Objects { get; set; } = new();
        public string? Skybox { get; set; }
        public Vector3 LightDirection { get; set; } = new(1, -1, 0);
        public float LightIntensity { get; set; } = 1.0f;
    }
}
