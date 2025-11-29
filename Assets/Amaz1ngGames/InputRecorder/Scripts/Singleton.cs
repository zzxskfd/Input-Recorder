using UnityEngine;

namespace Amaz1ngGames.InputRecorder
{
    /// <summary>
    /// Singleton class
    /// </summary>
    /// <typeparam name="T">Type of the singleton</typeparam>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        protected static T m_instance;

        /// <summary>
        /// The static reference to the instance
        /// </summary>
        public static T Instance
        {
            get
            {
                if (m_instance != null)
                    return m_instance;
                m_instance = FindFirstObjectByType<T>();
                if (m_instance == null)
                {
                    string name = typeof(T).FullName;
                    Debug.Log($"Can't find instance of ({name}), creating one");
                    var obj = new GameObject(name);
                    m_instance = obj.AddComponent<T>();
                }
                return m_instance;
            }
        }

        /// <summary>
        /// Gets whether an instance of this singleton exists
        /// </summary>
        public static bool InstanceExists
        {
            get { return m_instance != null; }
        }

        /// <summary>
        /// Awake method to associate singleton with instance
        /// </summary>
        protected virtual void Awake()
        {
            if (InstanceExists && m_instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                m_instance = (T) this;
            }
        }

        /// <summary>
        /// OnDestroy method to clear singleton association
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (m_instance == this)
            {
                m_instance = null;
            }
        }
    }
}
