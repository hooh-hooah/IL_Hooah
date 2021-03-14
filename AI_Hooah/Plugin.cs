using BepInEx;
using HarmonyLib;
using HooahComponents.Hooks;

[BepInPlugin(GUID, "AI_Hooah", VERSION)]
public class HooahPlugin : BaseUnityPlugin
{
    public const string GUID = "com.hooh.hooah";
    public const string VERSION = "1.4.3";

    private void Start()
    {
        Harmony.CreateAndPatchAll(typeof(Hooks));
        SkinnedAccessoryHook.Logger = Logger;
        SkinnedAccessoryHook.RegisterHook();
    }
}
