namespace AkariDash.Core.Gpus.NvApi;

/// <summary>An NVAPI call that returned anything but NVAPI_OK, with NVIDIA's reason in the message.</summary>
public sealed class NvApiException(string function, int status)
    : Exception($"NVIDIA {function} failed: {NvApiStatus.Describe(status)} (status {status}).")
{
    /// <summary>The function, as NVIDIA names it without "NvAPI_".</summary>
    public string Function { get; } = function;

    /// <summary>The NvAPI_Status it returned.</summary>
    public int Status { get; } = status;
}
