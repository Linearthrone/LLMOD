using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HouseVictoria.Services.SystemMonitor
{
    /// <summary>
    /// Wrapper for NVIDIA Management Library (NVML) using P/Invoke
    /// Requires NVIDIA drivers with nvml.dll to be installed
    /// </summary>
    internal static class NvmlWrapper
    {
        private const string NvmlDllName = "nvml.dll";
        private static bool _isInitialized = false;
        private static bool _isAvailable = false;
        private static readonly object _lockObject = new object();

        // NVML Return codes
        private const int NvmlSuccess = 0;
        private const int NvmlErrorNotSupported = -8;
        private const int NvmlErrorNoPermission = -3;
        private const int NvmlErrorNotInitialized = -1;
        private const int NvmlErrorInitializationFailed = -2;
        private const int NvmlErrorDriverNotLoaded = -4;
        private const int NvmlErrorNotFound = -6;

        /// <summary>
        /// Check if NVML is available on this system
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                if (!_isInitialized)
                {
                    Initialize();
                }
                return _isAvailable;
            }
        }

        /// <summary>
        /// Initialize NVML library
        /// </summary>
        private static void Initialize()
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                    return;

                try
                {
                    // Try to load the DLL
                    var result = nvmlInit_v2();
                    if (result == NvmlSuccess)
                    {
                        _isAvailable = true;
                        _isInitialized = true;
                        System.Diagnostics.Debug.WriteLine("NVML initialized successfully");
                    }
                    else
                    {
                        _isAvailable = false;
                        _isInitialized = true;
                        System.Diagnostics.Debug.WriteLine($"NVML initialization failed with error code: {result}");
                    }
                }
                catch (DllNotFoundException)
                {
                    _isAvailable = false;
                    _isInitialized = true;
                    System.Diagnostics.Debug.WriteLine("NVML DLL not found. NVIDIA drivers may not be installed.");
                }
                catch (Exception ex)
                {
                    _isAvailable = false;
                    _isInitialized = true;
                    System.Diagnostics.Debug.WriteLine($"NVML initialization exception: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Shutdown NVML library
        /// </summary>
        public static void Shutdown()
        {
            if (!_isAvailable || !_isInitialized)
                return;

            try
            {
                nvmlShutdown();
                _isAvailable = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NVML shutdown exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the number of NVIDIA GPUs in the system
        /// </summary>
        public static int GetDeviceCount()
        {
            if (!_isAvailable)
                return 0;

            try
            {
                var result = nvmlDeviceGetCount_v2(out uint count);
                if (result == NvmlSuccess)
                {
                    return (int)count;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting device count: {ex.Message}");
            }
            return 0;
        }

        /// <summary>
        /// Get GPU temperature in Celsius
        /// </summary>
        public static double GetTemperature(int deviceIndex)
        {
            if (!_isAvailable)
                return 0.0;

            try
            {
                var result = nvmlDeviceGetHandleByIndex_v2((uint)deviceIndex, out nvmlDevice_t device);
                if (result != NvmlSuccess)
                {
                    return 0.0;
                }

                result = nvmlDeviceGetTemperature(device, nvmlTemperatureSensors_t.NVML_TEMPERATURE_GPU, out uint temp);
                if (result == NvmlSuccess)
                {
                    return temp;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting GPU temperature: {ex.Message}");
            }
            return 0.0;
        }

        /// <summary>
        /// Get GPU fan speed as a percentage (0-100)
        /// </summary>
        public static double GetFanSpeed(int deviceIndex)
        {
            if (!_isAvailable)
                return 0.0;

            try
            {
                var result = nvmlDeviceGetHandleByIndex_v2((uint)deviceIndex, out nvmlDevice_t device);
                if (result != NvmlSuccess)
                {
                    return 0.0;
                }

                result = nvmlDeviceGetFanSpeed(device, out uint speed);
                if (result == NvmlSuccess)
                {
                    // NVML returns fan speed as a percentage (0-100)
                    return speed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting GPU fan speed: {ex.Message}");
            }
            return 0.0;
        }

        /// <summary>
        /// Get GPU fan speed in RPM (if supported)
        /// Note: RPM is only available for S-class devices. For other GPUs, returns 0.0
        /// Use GetFanSpeedPercentage() and convert to RPM estimate if RPM not available
        /// </summary>
        public static double GetFanSpeedRpm(int deviceIndex)
        {
            if (!_isAvailable)
                return 0.0;

            try
            {
                var result = nvmlDeviceGetHandleByIndex_v2((uint)deviceIndex, out nvmlDevice_t device);
                if (result != NvmlSuccess)
                {
                    return 0.0;
                }

                // Try to get fan speed RPM (only available for S-class devices)
                result = nvmlDeviceGetFanSpeed_v2(device, 0, out uint speedRpm);
                if (result == NvmlSuccess && speedRpm > 0)
                {
                    // If successful, verify it's actually RPM and not percentage
                    // RPM values are typically much higher than 100, while percentage is 0-100
                    if (speedRpm > 100)
                    {
                        return speedRpm;
                    }
                }

                // For non-S-class devices, v2 function may return percentage instead of RPM
                // The value will be 0-100 if percentage, or > 100 if RPM
                // We already checked above, so if we get here, it's likely percentage
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting GPU fan speed RPM: {ex.Message}");
            }
            return 0.0;
        }

        /// <summary>
        /// Get GPU fan speed as a percentage (0-100, may exceed 100 for some GPUs)
        /// This is the most widely supported method across all NVIDIA GPUs
        /// </summary>
        public static double GetFanSpeedPercentage(int deviceIndex)
        {
            return GetFanSpeed(deviceIndex);
        }

        /// <summary>
        /// Get GPU utilization (usage percentage)
        /// </summary>
        public static double GetUtilization(int deviceIndex)
        {
            if (!_isAvailable)
                return 0.0;

            try
            {
                var result = nvmlDeviceGetHandleByIndex_v2((uint)deviceIndex, out nvmlDevice_t device);
                if (result != NvmlSuccess)
                {
                    return 0.0;
                }

                result = nvmlDeviceGetUtilizationRates(device, out nvmlUtilization_t utilization);
                if (result == NvmlSuccess)
                {
                    // Return GPU utilization percentage
                    return utilization.gpu;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting GPU utilization: {ex.Message}");
            }
            return 0.0;
        }

        // P/Invoke declarations for NVML functions

        [DllImport(NvmlDllName, EntryPoint = "nvmlInit_v2")]
        private static extern int nvmlInit_v2();

        [DllImport(NvmlDllName, EntryPoint = "nvmlShutdown")]
        private static extern int nvmlShutdown();

        [DllImport(NvmlDllName, EntryPoint = "nvmlDeviceGetCount_v2")]
        private static extern int nvmlDeviceGetCount_v2(out uint deviceCount);

        [DllImport(NvmlDllName, EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
        private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out nvmlDevice_t device);

        [DllImport(NvmlDllName, EntryPoint = "nvmlDeviceGetTemperature")]
        private static extern int nvmlDeviceGetTemperature(nvmlDevice_t device, nvmlTemperatureSensors_t sensorType, out uint temp);

        [DllImport(NvmlDllName, EntryPoint = "nvmlDeviceGetFanSpeed")]
        private static extern int nvmlDeviceGetFanSpeed(nvmlDevice_t device, out uint speed);

        [DllImport(NvmlDllName, EntryPoint = "nvmlDeviceGetFanSpeed_v2")]
        private static extern int nvmlDeviceGetFanSpeed_v2(nvmlDevice_t device, uint fan, out uint speed);

        [DllImport(NvmlDllName, EntryPoint = "nvmlDeviceGetUtilizationRates")]
        private static extern int nvmlDeviceGetUtilizationRates(nvmlDevice_t device, out nvmlUtilization_t utilization);

        // NVML types

        [StructLayout(LayoutKind.Sequential)]
        private struct nvmlDevice_t
        {
            public IntPtr Handle;
        }

        private enum nvmlTemperatureSensors_t
        {
            NVML_TEMPERATURE_GPU = 0
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct nvmlUtilization_t
        {
            public uint gpu;      // GPU utilization percentage (0-100)
            public uint memory;   // Memory utilization percentage (0-100)
        }
    }
}
