using BepInEx;
using BepInEx.Harmony;
using HarmonyLib;
using HooahComponents.Hooks;

[BepInPlugin(GUID, "HS2_Hooah", VERSION)]
public class HooahPlugin : BaseUnityPlugin
{
    public const string GUID = "com.hooh.hooah";
    public const string VERSION = "1.4.0";

    private void Start()
    {
        Harmony.CreateAndPatchAll(typeof(Hooks));
        SkinnedAccessoryHook.RegisterHook();
    }
}