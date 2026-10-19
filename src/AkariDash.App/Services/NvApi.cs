using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AkariDash.Core.Gpus.NvApi;

namespace AkariDash.App.Services;

/// <summary>
/// NVIDIA's driver API, reached the way Vain Toolbox reaches it: load nvapi64.dll, take its one export,
/// nvapi_QueryInterface, and ask it for each function by the id in NVIDIA's nvapi_interface.h
/// (<see cref="NvApiIds"/>). Every call that does not return NVAPI_OK throws <see cref="NvApiException"/>.
/// Null on a PC without an NVIDIA driver.
/// </summary>
internal sealed class NvApi
{
    private static readonly Lazy<NvApi?> Shared = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly QueryInterface _query;
    private readonly ConcurrentDictionary<uint, Delegate> _functions = new();

    private NvApi(QueryInterface query) => _query = query;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NoArguments();

    /// <summary>NVAPI, loaded and initialised once per run; null when the driver has none to give.</summary>
    public static NvApi? Instance => Shared.Value;

    /// <summary>
    /// The driver function behind <paramref name="id"/> as <typeparamref name="T"/>; throws when this driver
    /// does not have it (NVAPI_NO_IMPLEMENTATION).
    /// </summary>
    public T Function<T>(uint id, string name) where T : Delegate =>
        (T)_functions.GetOrAdd(id, _ =>
        {
            var pointer = _query(id);
            return pointer == IntPtr.Zero
                ? throw new NvApiException(name, NvApiStatus.NoImplementation)
                : Marshal.GetDelegateForFunctionPointer<T>(pointer);
        });

    /// <summary>Throws <see cref="NvApiException"/> for <paramref name="function"/> unless it returned NVAPI_OK.</summary>
    public static void Check(string function, int status)
    {
        if (status != NvApiStatus.Ok)
        {
            throw new NvApiException(function, status);
        }
    }

    private static NvApi? Load()
    {
        if (!NativeLibrary.TryLoad("nvapi64.dll", out var library) ||
            !NativeLibrary.TryGetExport(library, "nvapi_QueryInterface", out var export))
        {
            return null;
        }

        var api = new NvApi(Marshal.GetDelegateForFunctionPointer<QueryInterface>(export));
        try
        {
            Check("Initialize", api.Function<NoArguments>(NvApiIds.Initialize, "Initialize")());
            return api;
        }
        catch (NvApiException)
        {
            return null;
        }
    }
}
