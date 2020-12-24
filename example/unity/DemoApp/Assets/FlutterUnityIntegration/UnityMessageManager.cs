using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using UnityEngine.Scripting;
using UnityEngine;
using UnityEngine.SceneManagement;

[Preserve]
[Serializable]
public class MessageHandler
{
    public int id;
    public string seq;
    public string name;
    public string data;

    public static MessageHandler Deserialize(string message) =>
        JsonUtility.FromJson<MessageHandler>(message);

    // ReSharper disable once InconsistentNaming
    public T getData<T>() => string.IsNullOrEmpty(data)
        ? default(T)
        : JsonUtility.FromJson<T>(data);
    // ReSharper disable once InconsistentNaming
    public void send(object value) =>
        UnityMessageManager.Instance.SendMessageToFlutter(
            UnityMessageManager.MessagePrefix +
            JsonUtility.ToJson(new MessageHandler
            {
                id = id,
                name = name,
                seq = "end",
                data = JsonUtility.ToJson(value)
            }));
}

[Preserve]
[Serializable]
public class UnityMessage
{
    public string name;
    public string data;
    [IgnoreDataMember] public Action<object> callBack;
}

#if UNITY_IOS || UNITY_TVOS
public class NativeAPI
{
    [DllImport("__Internal")]
    public static extern void onUnityMessage(string message);
    
    /* [DllImport("__Internal")]
    public static extern void showHostMainWindow();
    
    [DllImport("__Internal")]
    public static extern void unloadPlayer();
    
    [DllImport("__Internal")]
    public static extern void quitPlayer(); */

    [DllImport("__Internal")]
    public static extern void onUnitySceneLoaded(string name, int buildIndex, bool isLoaded, bool IsValid);
}
#endif

public class UnityMessageManager : MonoBehaviour
{
    /* #if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void onUnityMessage(string message);
        [DllImport("__Internal")]
        public static extern void onUnitySceneLoaded(string name, int buildIndex, bool isLoaded, bool IsValid);
    #endif */

    public const string MessagePrefix = "@UnityMessage@";
    private static int ID = 0;

    private static int generateId()
    {
        ID += 1;
        return ID;
    }

    public static UnityMessageManager Instance { get; }

    public delegate void MessageDelegate(string message);
    public event MessageDelegate OnMessage;

    public delegate void MessageHandlerDelegate(MessageHandler handler);
    public event MessageHandlerDelegate OnFlutterMessage;

    private readonly Dictionary<int, UnityMessage> _waitCallbackMessageMap = new Dictionary<int, UnityMessage>();

    static UnityMessageManager()
    {
        GameObject go = new GameObject("UnityMessageManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<UnityMessageManager>();
    }

    private void Awake()
    {
    }
    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log(scene);
#if UNITY_ANDROID
        try
        {
            AndroidJavaClass jc = new AndroidJavaClass("com.xraph.plugins.flutterunitywidget.UnityUtils");
            jc.CallStatic("onUnitySceneLoaded", scene.name, scene.buildIndex, scene.isLoaded, scene.IsValid());
        }
        catch (Exception e)
        {
            print(e.Message);
        }
#elif UNITY_IOS && !UNITY_EDITOR
            NativeAPI.onUnitySceneLoaded(scene.name, scene.buildIndex, scene.isLoaded, scene.IsValid());
#endif
    }

    public void ShowHostMainWindow()
    {
#if UNITY_ANDROID
        try
        {
            AndroidJavaClass jc = new AndroidJavaClass("com.xraph.plugins.flutterunitywidget.OverrideUnityActivity");
            AndroidJavaObject overrideActivity = jc.GetStatic<AndroidJavaObject>("instance");
            overrideActivity.Call("showMainActivity");
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
#elif UNITY_IOS || UNITY_TVOS
        // NativeAPI.showHostMainWindow();
#endif
    }
    public void UnloadMainWindow()
    {
#if UNITY_ANDROID
        try
        {
            AndroidJavaClass jc = new AndroidJavaClass("com.xraph.plugins.flutterunitywidget.OverrideUnityActivity");
            AndroidJavaObject overrideActivity = jc.GetStatic<AndroidJavaObject>("instance");
            overrideActivity.Call("unloadPlayer");
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
#elif UNITY_IOS || UNITY_TVOS
        // NativeAPI.unloadPlayer();
#endif
    }
    public void QuitUnityWindow()
    {
#if UNITY_ANDROID
        try
        {
            AndroidJavaClass jc = new AndroidJavaClass("com.xraph.plugins.flutterunitywidget.OverrideUnityActivity");
            AndroidJavaObject overrideActivity = jc.GetStatic<AndroidJavaObject>("instance");
            overrideActivity.Call("quitPlayer");
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
#elif UNITY_IOS || UNITY_TVOS
        // NativeAPI.quitPlayer();
#endif
    }
    public void SendMessageToFlutter(string message)
    {
        #if UNITY_ANDROID
            try
            {
                AndroidJavaClass jc = new AndroidJavaClass("com.xraph.plugins.flutterunitywidget.UnityUtils");
                jc.CallStatic("onUnityMessage", message);
            }
            catch (Exception e)
            {
                print(e.Message);
            }
        #elif UNITY_IOS && !UNITY_EDITOR
            NativeAPI.onUnityMessage(message);
        #endif
    }
    public void SendMessageToFlutter(UnityMessage message)
    {
        int id = generateId();
        if (message.callBack != null)
            _waitCallbackMessageMap.Add(id, message);

        SendMessageToFlutter(
            MessagePrefix +
            JsonUtility.ToJson(new MessageHandler
            {
                id = id,
                seq = message.callBack != null ? "start" : string.Empty,
                name = message.name,
                data = message.data
            }));
    }

    private void onMessage(string message)
    {
        OnMessage?.Invoke(message);
    }
    private void onFlutterMessage(string message)
    {
        if (!message.StartsWith(MessagePrefix))
            return;
        message = message.Replace(MessagePrefix, "");

        var handler = MessageHandler.Deserialize(message);
        if ("end".Equals(handler.seq))
        {
            // handle callback message
            if (_waitCallbackMessageMap.TryGetValue(handler.id, out var m))
            {
                _waitCallbackMessageMap.Remove(handler.id);
                m.callBack?.Invoke(handler.getData<object>()); // todo
            }
            return;
        }
        OnFlutterMessage?.Invoke(handler);
    }
}