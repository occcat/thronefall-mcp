namespace UnityEngine
{
    public class Object { }

    public class Component : Object { }

    public class Behaviour : Component { }

    public class MonoBehaviour : Behaviour
    {
        public GameObject gameObject => null!;
    }

    public class GameObject : Object { }

    public static class Time
    {
        public static int frameCount { get; set; }
        public static float unscaledTime { get; set; }
        public static float realtimeSinceStartup { get; set; }
    }
}
