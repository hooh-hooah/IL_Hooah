using BepInEx;
using BepInEx.Harmony;
using HooahComponents.Hooks;

[BepInPlugin(GUID, "HS2_Hooah", VERSION)]
public class HooahPlugin : BaseUnityPlugin
{
    public const string GUID = "com.hooh.hooah";
    public const string VERSION = "1.0.0";

    private void Start()
    {
        HarmonyWrapper.PatchAll(typeof(Hooks));
        SkinnedAccessoryHook.RegisterHook();
    }
}