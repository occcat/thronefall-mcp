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
}
