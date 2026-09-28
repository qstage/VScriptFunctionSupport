using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using Microsoft.Extensions.Logging;

namespace VScriptFunctionSupport;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct ScriptFuncDescriptor_t
{
    public nint m_pszScriptName;
    public nint m_pszFunction;
    public nint m_pszDescription;
    public byte m_ReturnType;
    public byte m_iVariantCount;
    public byte m_iParamCount;
    public fixed byte m_Parameters[12];
    public nint m_pszParameterNames;
};

[StructLayout(LayoutKind.Sequential)]
public struct CUtlMemory<T> where T : unmanaged
{
    public unsafe T* m_pMemory;
    public int m_nAllocationCount;
    public int m_nGrowSize;
}

[StructLayout(LayoutKind.Sequential)]
public struct CUtlVector<T> where T : unmanaged
{
    public unsafe T this[int index]
    {
        get => m_Memory.m_pMemory[index];
        set => m_Memory.m_pMemory[index] = value;
    }

    public int m_iSize;
    public CUtlMemory<T> m_Memory;

    public T Element(int index) => this[index];
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct ScriptFunctionBindingCurrent_t
{
    public ScriptFuncDescriptor_t m_desc;
    public ScriptClassDesc_t* m_pClassDesc;
    public nint m_pfnBinding;
    public nint m_pFunction;
    public int m_flags;
};

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct ScriptClassDesc_t
{
    public nint m_pszScriptName;
    public nint m_pszClassname;
    public nint m_pszDescription;
    public ScriptClassDesc_t* m_pBaseDesc;
    public CUtlVector<ScriptFunctionBindingCurrent_t> m_FunctionBindings;

    public nint m_pfnConstruct;
    public nint m_pfnDestruct;
    public nint pHelper;
};

public class Plugin : BasePlugin
{
    public override string ModuleName => "VScriptFunctionSupport";
    public override string ModuleVersion => "1.0.0";

    // Example functions
    public delegate int GetHealthFunc(nint @this);
    public GetHealthFunc GetHealth = null!;

    public delegate void SetGravityFunc(nint @this, float gravity);
    public SetGravityFunc SetGravity = null!;

    public delegate void SetLocalScaleFunc(nint @this, float scale);
    public SetLocalScaleFunc SetLocalScale = null!;

    public delegate void SetModelScaleFunc(nint @this, float scale);
    public SetModelScaleFunc SetModelScale = null!;

    public delegate void SetSizeFunc(nint @this, nint min, nint max);
    public SetSizeFunc SetSize = null!;

    public delegate float GetLocalScaleFunc(nint @this);
    public GetLocalScaleFunc GetLocalScale = null!;

    public delegate float GetAbsScaleFunc(nint @this);
    public GetAbsScaleFunc GetAbsScale = null!;

    private readonly Dictionary<string, nint> m_pVScriptFunctions = [];

    public override void Load(bool hotReload)
    {
        InitVScriptFuncs();

        if (!ResolveVScriptFunction("GetHealth", out GetHealth!))
        {
            Logger.LogError("Not found GetHealth");
        }

        if (!ResolveVScriptFunction("SetGravity", out SetGravity!))
        {
            Logger.LogError("Not found SetGravity");
        }

        if (!ResolveVScriptFunction("GetLocalScale", out GetLocalScale!))
        {
            Logger.LogError("Not found GetLocalScale");
        }

        if (!ResolveVScriptFunction("SetLocalScale", out SetLocalScale!))
        {
            Logger.LogError("Not found SetLocalScale");
        }

        if (!ResolveVScriptFunction("GetAbsScale", out GetAbsScale!))
        {
            Logger.LogError("Not found GetAbsScale");
        }

        if (!ResolveVScriptFunction("SetModelScale", out SetModelScale!))
        {
            Logger.LogError("Not found SetModelScale");
        }

        if (!ResolveVScriptFunction("SetSize", out SetSize!))
        {
            Logger.LogError("Not found SetSize");
        }
    }

    public bool ResolveVScriptFunction<T>(string funcName, out T? func) where T : Delegate
    {
        func = null;

        if (m_pVScriptFunctions.TryGetValue(funcName, out nint ptr))
        {
            func = Marshal.GetDelegateForFunctionPointer<T>(ptr);
            return true;
        }

        return false;
    }

    [ConsoleCommand("css_vscript_dump"), CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void OnCommandVScriptFuncsDump(CCSPlayerController? _, CommandInfo info)
    {
        int index = 1;
        foreach (var func in m_pVScriptFunctions)
        {
            Logger.LogInformation("[{idx}] {funcName} | {addr:X}", index++, func.Key, func.Value);
        }
    }

    private unsafe void InitVScriptFuncs()
    {
        var pEntity = new VTable("CBaseEntity");

        var pScriptClasses = new Span<ScriptClassDesc_t>((void*)pEntity.GetFunctionWithReturn<nint, nint>(3).Invoke(pEntity.Handle), 2);

        var pScriptClass_CBaseEntity = pScriptClasses[0];
        var pScriptClass_CBaseModelEntity = pScriptClasses[1];

        for (int i = 0; i < pScriptClass_CBaseEntity.m_FunctionBindings.m_iSize; i++)
        {
            var funcName = Marshal.PtrToStringUTF8(pScriptClass_CBaseEntity.m_FunctionBindings[i].m_desc.m_pszScriptName);
            if (funcName != null)
                m_pVScriptFunctions.Add(funcName, pScriptClass_CBaseEntity.m_FunctionBindings[i].m_pFunction);
        }

        for (int i = 0; i < pScriptClass_CBaseModelEntity.m_FunctionBindings.m_iSize; i++)
        {
            var funcName = Marshal.PtrToStringUTF8(pScriptClass_CBaseModelEntity.m_FunctionBindings[i].m_desc.m_pszScriptName);
            if (funcName != null)
                m_pVScriptFunctions.Add(funcName, pScriptClass_CBaseModelEntity.m_FunctionBindings[i].m_pFunction);
        }
    }
}
