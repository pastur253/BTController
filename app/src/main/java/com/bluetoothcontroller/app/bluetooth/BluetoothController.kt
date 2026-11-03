package com.bluetoothcontroller.app.bluetooth

import android.annotation.SuppressLint
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothClass
import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothGatt
import android.bluetooth.BluetoothGattCallback
import android.bluetooth.BluetoothManager
import android.bluetooth.BluetoothProfile
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.os.Build
import android.util.Log
import androidx.core.content.ContextCompat
import com.bluetoothcontroller.app.data.BluetoothDeviceModel
import com.bluetoothcontroller.app.data.DeviceType
import com.bluetoothcontroller.app.data.WidgetPreferences
import com.bluetoothcontroller.app.widget.BluetoothWidgetProvider
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import java.lang.reflect.Method

@SuppressLint("MissingPermission")
class BluetoothController(private val context: Context) {

    private val tag = "BluetoothController"
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val widgetPrefs = WidgetPreferences(context)
    private val refreshMutex = Mutex()

    private val bluetoothManager: BluetoothManager? =
        context.getSystemService(Context.BLUETOOTH_SERVICE) as? BluetoothManager
    val bluetoothAdapter: BluetoothAdapter? = bluetoothManager?.adapter

    // Active Profile Proxies
    private var a2dpProxy: BluetoothProfile? = null
    private var headsetProxy: BluetoothProfile? = null
    private var hidProxy: BluetoothProfile? = null

    // Active GATT connections for BLE devices
    private val gattMap = mutableMapOf<String, BluetoothGatt>()

    // In-flight connecting & disconnecting addresses
    private val _connectingAddresses = MutableStateFlow<Set<String>>(emptySet())
    val connectingAddresses: StateFlow<Set<String>> = _connectingAddresses.asStateFlow()

    private val _disconnectingAddresses = MutableStateFlow<Set<String>>(emptySet())
    val disconnectingAddresses: StateFlow<Set<String>> = _disconnectingAddresses.asStateFlow()

    private val _isBluetoothEnabled = MutableStateFlow(bluetoothAdapter?.isEnabled == true)
    val isBluetoothEnabled: StateFlow<Boolean> = _isBluetoothEnabled.asStateFlow()

    private val _isScanning = MutableStateFlow(false)
    val isScanning: StateFlow<Boolean> = _isScanning.asStateFlow()

    // Paired devices NOT in widget (bottom list)
    private val _pairedDevices = MutableStateFlow<List<BluetoothDeviceModel>>(emptyList())
    val pairedDevices: StateFlow<List<BluetoothDeviceModel>> = _pairedDevices.asStateFlow()

    // Devices added to widget (top list, ordered)
    private val _widgetDevices = MutableStateFlow<List<BluetoothDeviceModel>>(emptyList())
    val widgetDevices: StateFlow<List<BluetoothDeviceModel>> = _widgetDevices.asStateFlow()

    private val _discoveredDevices = MutableStateFlow<List<BluetoothDeviceModel>>(emptyList())
    val discoveredDevices: StateFlow<List<BluetoothDeviceModel>> = _discoveredDevices.asStateFlow()

    // Cached reflection method
    private val isConnectedMethod: Method? by lazy {
        findMethod(BluetoothDevice::class.java, "isConnected")
    }

    private val profileListener = object : BluetoothProfile.ServiceListener {
        override fun onServiceConnected(profile: Int, proxy: BluetoothProfile?) {
            when (profile) {
                BluetoothProfile.A2DP -> a2dpProxy = proxy
                BluetoothProfile.HEADSET -> headsetProxy = proxy
                4 -> hidProxy = proxy // BluetoothProfile.HID_HOST
            }
            refreshDevices()
        }

        override fun onServiceDisconnected(profile: Int) {
            when (profile) {
                BluetoothProfile.A2DP -> a2dpProxy = null
                BluetoothProfile.HEADSET -> headsetProxy = null
                4 -> hidProxy = null
            }
            refreshDevices()
        }
    }

    private val scanReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            when (intent?.action) {
                BluetoothDevice.ACTION_FOUND -> {
                    val device: BluetoothDevice? = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                        intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE, BluetoothDevice::class.java)
                    } else {
                        @Suppress("DEPRECATION")
                        intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE)
                    }
                    device?.let { addDiscoveredDevice(it) }
                }
                BluetoothDevice.ACTION_NAME_CHANGED,
                "android.bluetooth.device.action.ALIAS_CHANGED" -> {
                    val device: BluetoothDevice? = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                        intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE, BluetoothDevice::class.java)
                    } else {
                        @Suppress("DEPRECATION")
                        intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE)
                    }
                    val extraName = intent.getStringExtra(BluetoothDevice.EXTRA_NAME)
                    if (device != null && !extraName.isNullOrBlank()) {
                        widgetPrefs.saveDeviceName(device.address, extraName)
                    }
                    refreshDevices()
                }
                BluetoothDevice.ACTION_BOND_STATE_CHANGED -> {
                    refreshDevices()
                }
                BluetoothAdapter.ACTION_DISCOVERY_STARTED -> {
                    _isScanning.value = true
                }
                BluetoothAdapter.ACTION_DISCOVERY_FINISHED -> {
                    _isScanning.value = false
                }
            }
        }
    }

    init {
        initProfiles()
        registerScanReceiver()
        refreshDevices()
    }

    fun hasPermissions(): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            ContextCompat.checkSelfPermission(context, android.Manifest.permission.BLUETOOTH_CONNECT) ==
                    PackageManager.PERMISSION_GRANTED &&
                    ContextCompat.checkSelfPermission(context, android.Manifest.permission.BLUETOOTH_SCAN) ==
                    PackageManager.PERMISSION_GRANTED
        } else {
            ContextCompat.checkSelfPermission(context, android.Manifest.permission.BLUETOOTH) ==
                    PackageManager.PERMISSION_GRANTED
        }
    }

    fun initProfiles() {
        if (!hasPermissions() || bluetoothAdapter == null) return
        try {
            bluetoothAdapter.getProfileProxy(context, profileListener, BluetoothProfile.A2DP)
            bluetoothAdapter.getProfileProxy(context, profileListener, BluetoothProfile.HEADSET)
            bluetoothAdapter.getProfileProxy(context, profileListener, 4) // HID_HOST
        } catch (e: Exception) {
            Log.e(tag, "Failed to get profile proxies", e)
        }
    }

    private fun registerScanReceiver() {
        val filter = IntentFilter().apply {
            addAction(BluetoothDevice.ACTION_FOUND)
            addAction(BluetoothDevice.ACTION_NAME_CHANGED)
            addAction("android.bluetooth.device.action.ALIAS_CHANGED")
            addAction(BluetoothDevice.ACTION_BOND_STATE_CHANGED)
            addAction(BluetoothAdapter.ACTION_DISCOVERY_STARTED)
            addAction(BluetoothAdapter.ACTION_DISCOVERY_FINISHED)
        }
        try {
            context.registerReceiver(scanReceiver, filter)
        } catch (e: Exception) {
            Log.e(tag, "Failed to register scanReceiver", e)
        }
    }

    fun getDeviceDisplayName(device: BluetoothDevice): String {
        val address = device.address

        // 1. In Android 11+ (API 30+), getAlias() returns the user-customized name from Settings
        val alias: String? = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            try {
                device.alias
            } catch (_: SecurityException) {
                null
            } catch (_: Throwable) {
                null
            }
        } else {
            try {
                val method = device.javaClass.getMethod("getAlias")
                method.invoke(device) as? String
            } catch (_: Throwable) {
                null
            }
        }

        // 2. Hardware / broadcasted Bluetooth name
        val rawName: String? = try {
            device.name
        } catch (_: SecurityException) {
            null
        } catch (_: Throwable) {
            null
        }

        val effectiveName = alias?.trim()?.takeIf { it.isNotEmpty() }
            ?: rawName?.trim()?.takeIf { it.isNotEmpty() }
            ?: widgetPrefs.getDeviceName(address)
            ?: address

        if (effectiveName != address) {
            widgetPrefs.saveDeviceName(address, effectiveName)
        }

        return effectiveName
    }

    fun getWidgetDevicesSnapshot(forceRefresh: Boolean = false): List<BluetoothDeviceModel> {
        if (!forceRefresh) {
            val current = _widgetDevices.value
            if (current.isNotEmpty()) {
                return current
            }
        }

        val widgetAddresses = widgetPrefs.getWidgetDeviceAddresses()
        if (widgetAddresses.isEmpty()) return emptyList()

        val bonded = try {
            if (hasPermissions()) bluetoothAdapter?.bondedDevices ?: emptySet() else emptySet()
        } catch (_: Exception) {
            emptySet()
        }
        val bondedMap = bonded.associateBy { it.address }
        val connecting = _connectingAddresses.value
        val disconnecting = _disconnectingAddresses.value

        val list = widgetAddresses.map { addr ->
            val device = bondedMap[addr]
            val name = if (device != null) {
                getDeviceDisplayName(device)
            } else {
                widgetPrefs.getDeviceName(addr) ?: addr
            }
            val isConnected = device?.let { isDeviceConnected(it) } ?: false
            val deviceType = device?.let { resolveDeviceType(it) } ?: DeviceType.OTHER

            BluetoothDeviceModel(
                address = addr,
                name = name,
                isConnected = isConnected,
                isConnecting = connecting.contains(addr),
                isDisconnecting = disconnecting.contains(addr),
                isInWidget = true,
                deviceType = deviceType
            )
        }
        _widgetDevices.value = list
        return list
    }

    fun refreshDevices() {
        scope.launch {
            refreshMutex.withLock {
                _isBluetoothEnabled.value = bluetoothAdapter?.isEnabled == true
                if (!hasPermissions() || bluetoothAdapter == null || !bluetoothAdapter.isEnabled) {
                    _pairedDevices.value = emptyList()
                    val widgetAddresses = widgetPrefs.getWidgetDeviceAddresses()
                    val fallbackList = widgetAddresses.map { addr ->
                        val savedName = widgetPrefs.getDeviceName(addr) ?: addr
                        BluetoothDeviceModel(
                            address = addr,
                            name = savedName,
                            isConnected = false,
                            isConnecting = false,
                            isDisconnecting = false,
                            isInWidget = true,
                            deviceType = DeviceType.OTHER
                        )
                    }
                    _widgetDevices.value = fallbackList
                    return@withLock
                }

                try {
                    val bonded = bluetoothAdapter.bondedDevices ?: emptySet()
                    val widgetAddresses = widgetPrefs.getWidgetDeviceAddresses()
                    val connecting = _connectingAddresses.value
                    val disconnecting = _disconnectingAddresses.value

                    val allDeviceModels = bonded.map { device ->
                        val address = device.address
                        val isConnected = isDeviceConnected(device)
                        val isConnecting = connecting.contains(address)
                        val isDisconnecting = disconnecting.contains(address)
                        val isInWidget = widgetAddresses.contains(address)
                        val displayName = getDeviceDisplayName(device)
                        val deviceType = resolveDeviceType(device)

                        BluetoothDeviceModel(
                            address = address,
                            name = displayName,
                            isConnected = isConnected,
                            isConnecting = isConnecting,
                            isDisconnecting = isDisconnecting,
                            isInWidget = isInWidget,
                            deviceType = deviceType
                        )
                    }

                    // Upper list: devices added to widget in exact user-defined order
                    val widgetList = widgetAddresses.mapNotNull { addr ->
                        allDeviceModels.find { it.address == addr } ?: run {
                            val savedName = widgetPrefs.getDeviceName(addr) ?: addr
                            BluetoothDeviceModel(
                                address = addr,
                                name = savedName,
                                isConnected = false,
                                isConnecting = connecting.contains(addr),
                                isDisconnecting = disconnecting.contains(addr),
                                isInWidget = true,
                                deviceType = DeviceType.OTHER
                            )
                        }
                    }
                    _widgetDevices.value = widgetList

                    // Lower list: only devices NOT added to widget
                    val unaddedList = allDeviceModels
                        .filter { !it.isInWidget }
                        .sortedWith(compareByDescending<BluetoothDeviceModel> { it.isConnected }.thenBy { it.name })

                    _pairedDevices.value = unaddedList

                } catch (e: Exception) {
                    Log.e(tag, "Error refreshing devices", e)
                }
            }
            try {
                BluetoothWidgetProvider.updateAllWidgets(context)
            } catch (e: Exception) {
                Log.e(tag, "Error updating widgets from refreshDevices", e)
            }
        }
    }

    fun isDeviceConnected(device: BluetoothDevice): Boolean {
        if (!hasPermissions()) return false
        try {
            // Check A2DP
            a2dpProxy?.let { proxy ->
                if (proxy.getConnectionState(device) == BluetoothProfile.STATE_CONNECTED) {
                    return true
                }
            }
            // Check Headset
            headsetProxy?.let { proxy ->
                if (proxy.getConnectionState(device) == BluetoothProfile.STATE_CONNECTED) {
                    return true
                }
            }
            // Check HID Host
            hidProxy?.let { proxy ->
                if (proxy.getConnectionState(device) == BluetoothProfile.STATE_CONNECTED) {
                    return true
                }
            }

            // Method reflection fallback: BluetoothDevice.isConnected()
            isConnectedMethod?.let {
                val connected = it.invoke(device) as? Boolean
                if (connected == true) return true
            }

        } catch (e: Exception) {
            Log.d(tag, "Exception checking connection state for ${device.address}: ${e.message}")
        }
        return false
    }

    fun toggleConnection(address: String) {
        val device = bluetoothAdapter?.getRemoteDevice(address) ?: return
        val currentlyConnected = isDeviceConnected(device)
        if (currentlyConnected) {
            disconnectDevice(address)
        } else {
            connectDevice(address)
        }
    }

    fun connectDevice(address: String) {
        val device = bluetoothAdapter?.getRemoteDevice(address) ?: return
        if (!hasPermissions()) return

        _connectingAddresses.value = _connectingAddresses.value + address
        _disconnectingAddresses.value = _disconnectingAddresses.value - address
        refreshDevices()

        scope.launch {
            if (a2dpProxy == null || headsetProxy == null) {
                initProfiles()
                delay(200)
            }

            var success = false

            // Try A2DP connect
            a2dpProxy?.let { proxy ->
                try {
                    val method = findMethod(proxy.javaClass, "connect", BluetoothDevice::class.java)
                    method?.let {
                        it.isAccessible = true
                        val res = it.invoke(proxy, device) as? Boolean
                        if (res == true) success = true
                        Log.d(tag, "A2DP connect result: $res")
                    }
                } catch (e: Exception) {
                    Log.d(tag, "A2DP connect failed: ${e.message}")
                }
            }

            // Try Headset connect
            headsetProxy?.let { proxy ->
                try {
                    val method = findMethod(proxy.javaClass, "connect", BluetoothDevice::class.java)
                    method?.let {
                        it.isAccessible = true
                        val res = it.invoke(proxy, device) as? Boolean
                        if (res == true) success = true
                        Log.d(tag, "Headset connect result: $res")
                    }
                } catch (e: Exception) {
                    Log.d(tag, "Headset connect failed: ${e.message}")
                }
            }

            // Try HID connect
            hidProxy?.let { proxy ->
                try {
                    val method = findMethod(proxy.javaClass, "connect", BluetoothDevice::class.java)
                    method?.let {
                        it.isAccessible = true
                        val res = it.invoke(proxy, device) as? Boolean
                        if (res == true) success = true
                        Log.d(tag, "HID connect result: $res")
                    }
                } catch (e: Exception) {
                    Log.d(tag, "HID connect failed: ${e.message}")
                }
            }

            // Connect GATT fallback
            if (!success) {
                try {
                    val gatt = device.connectGatt(context, false, object : BluetoothGattCallback() {
                        override fun onConnectionStateChange(gatt: BluetoothGatt?, status: Int, newState: Int) {
                            super.onConnectionStateChange(gatt, status, newState)
                            if (newState == BluetoothProfile.STATE_DISCONNECTED) {
                                gatt?.close()
                                gattMap.remove(address)
                            }
                            refreshDevices()
                        }
                    })
                    gattMap[address] = gatt
                } catch (e: Exception) {
                    Log.d(tag, "GATT connect failed: ${e.message}")
                }
            }

            // Poll until connected or max 3.5s
            var attempts = 0
            while (attempts < 7) {
                delay(500)
                attempts++
                if (isDeviceConnected(device)) {
                    break
                }
            }
            _connectingAddresses.value = _connectingAddresses.value - address
            refreshDevices()
        }
    }

    fun disconnectDevice(address: String) {
        val device = bluetoothAdapter?.getRemoteDevice(address) ?: return
        if (!hasPermissions()) return

        _disconnectingAddresses.value = _disconnectingAddresses.value + address
        _connectingAddresses.value = _connectingAddresses.value - address
        refreshDevices()

        scope.launch {
            if (a2dpProxy == null || headsetProxy == null) {
                initProfiles()
                delay(200)
            }

            var disconnected = false

            // 1. Disconnect A2DP
            a2dpProxy?.let { proxy ->
                try {
                    val method = findMethod(proxy.javaClass, "disconnect", BluetoothDevice::class.java)
                    method?.let {
                        it.isAccessible = true
                        val res = it.invoke(proxy, device) as? Boolean
                        if (res == true) disconnected = true
                        Log.d(tag, "A2DP disconnect result: $res")
                    }
                } catch (e: Exception) {
                    Log.d(tag, "A2DP disconnect failed: ${e.message}")
                }
            }

            // 2. Disconnect Headset
            headsetProxy?.let { proxy ->
                try {
                    val method = findMethod(proxy.javaClass, "disconnect", BluetoothDevice::class.java)
                    method?.let {
                        it.isAccessible = true
                        val res = it.invoke(proxy, device) as? Boolean
                        if (res == true) disconnected = true
                        Log.d(tag, "Headset disconnect result: $res")
                    }
                } catch (e: Exception) {
                    Log.d(tag, "Headset disconnect failed: ${e.message}")
                }
            }

            // 3. Disconnect HID
            hidProxy?.let { proxy ->
                try {
                    val method = findMethod(proxy.javaClass, "disconnect", BluetoothDevice::class.java)
                    method?.let {
                        it.isAccessible = true
                        val res = it.invoke(proxy, device) as? Boolean
                        if (res == true) disconnected = true
                        Log.d(tag, "HID disconnect result: $res")
                    }
                } catch (e: Exception) {
                    Log.d(tag, "HID disconnect failed: ${e.message}")
                }
            }

            // 4. Fallback on BluetoothDevice.disconnect()
            try {
                val method = findMethod(device.javaClass, "disconnect")
                method?.let {
                    it.isAccessible = true
                    it.invoke(device)
                    disconnected = true
                    Log.d(tag, "BluetoothDevice.disconnect() invoked")
                }
            } catch (e: Exception) {
                Log.d(tag, "BluetoothDevice.disconnect() failed: ${e.message}")
            }

            // 5. Disconnect GATT if active
            gattMap[address]?.let { gatt ->
                try {
                    gatt.disconnect()
                    gatt.close()
                } catch (e: Exception) {
                    Log.d(tag, "GATT disconnect failed: ${e.message}")
                }
                gattMap.remove(address)
            }

            // Poll until disconnected or max 2.5s
            var attempts = 0
            while (attempts < 5) {
                delay(500)
                attempts++
                if (!isDeviceConnected(device)) {
                    break
                }
            }
            _disconnectingAddresses.value = _disconnectingAddresses.value - address
            refreshDevices()
        }
    }

    private fun findMethod(clazz: Class<*>, name: String, vararg paramTypes: Class<*>): Method? {
        var current: Class<*>? = clazz
        while (current != null && current != Any::class.java) {
            try {
                return current.getDeclaredMethod(name, *paramTypes)
            } catch (_: NoSuchMethodException) {}
            try {
                return current.getMethod(name, *paramTypes)
            } catch (_: NoSuchMethodException) {}
            current = current.superclass
        }
        return null
    }

    fun addDeviceToWidget(address: String, name: String? = null) {
        widgetPrefs.addWidgetDevice(address, name)
        refreshDevices()
    }

    fun removeDeviceFromWidget(address: String) {
        widgetPrefs.removeWidgetDevice(address)
        refreshDevices()
    }

    fun moveWidgetDeviceUp(address: String) {
        if (widgetPrefs.moveUp(address)) {
            refreshDevices()
        }
    }

    fun moveWidgetDeviceDown(address: String) {
        if (widgetPrefs.moveDown(address)) {
            refreshDevices()
        }
    }

    fun reorderWidgetDevices(addresses: List<String>) {
        widgetPrefs.saveWidgetDeviceAddresses(addresses)
        refreshDevices()
    }

    fun clearTransitionState(address: String) {
        _connectingAddresses.value = _connectingAddresses.value - address
        _disconnectingAddresses.value = _disconnectingAddresses.value - address
    }

    fun startDiscovery() {
        if (!hasPermissions() || bluetoothAdapter == null) return
        _discoveredDevices.value = emptyList()
        if (bluetoothAdapter.isDiscovering) {
            bluetoothAdapter.cancelDiscovery()
        }
        bluetoothAdapter.startDiscovery()
        _isScanning.value = true
    }

    fun stopDiscovery() {
        if (!hasPermissions() || bluetoothAdapter == null) return
        if (bluetoothAdapter.isDiscovering) {
            bluetoothAdapter.cancelDiscovery()
        }
        _isScanning.value = false
    }

    fun pairDevice(address: String) {
        if (!hasPermissions() || bluetoothAdapter == null) return
        val device = bluetoothAdapter.getRemoteDevice(address) ?: return
        device.createBond()
    }

    private fun addDiscoveredDevice(device: BluetoothDevice) {
        val address = device.address
        val bondedAddresses = bluetoothAdapter?.bondedDevices?.map { it.address } ?: emptyList()
        if (bondedAddresses.contains(address)) return

        val currentList = _discoveredDevices.value.toMutableList()
        if (currentList.none { it.address == address }) {
            val name = getDeviceDisplayName(device).takeIf { it != address }
                ?: "Новое устройство (${address.takeLast(5)})"
            currentList.add(
                BluetoothDeviceModel(
                    address = address,
                    name = name,
                    isConnected = false,
                    isConnecting = false,
                    isDisconnecting = false,
                    isInWidget = widgetPrefs.isDeviceInWidget(address),
                    deviceType = resolveDeviceType(device)
                )
            )
            _discoveredDevices.value = currentList
        }
    }

    private fun resolveDeviceType(device: BluetoothDevice): DeviceType {
        val btClass = device.bluetoothClass ?: return DeviceType.OTHER
        return when (btClass.majorDeviceClass) {
            BluetoothClass.Device.Major.AUDIO_VIDEO -> {
                when (btClass.deviceClass) {
                    BluetoothClass.Device.AUDIO_VIDEO_WEARABLE_HEADSET,
                    BluetoothClass.Device.AUDIO_VIDEO_HEADPHONES -> DeviceType.HEADPHONES
                    BluetoothClass.Device.AUDIO_VIDEO_LOUDSPEAKER -> DeviceType.SPEAKER
                    else -> DeviceType.AUDIO_VIDEO
                }
            }
            BluetoothClass.Device.Major.PHONE -> DeviceType.PHONE
            BluetoothClass.Device.Major.COMPUTER -> DeviceType.COMPUTER
            BluetoothClass.Device.Major.WEARABLE -> DeviceType.WEARABLE
            BluetoothClass.Device.Major.PERIPHERAL -> DeviceType.PERIPHERAL
            else -> DeviceType.OTHER
        }
    }

    fun cleanup() {
        try {
            context.unregisterReceiver(scanReceiver)
        } catch (_: Exception) {}
        if (hasPermissions() && bluetoothAdapter?.isDiscovering == true) {
            bluetoothAdapter.cancelDiscovery()
        }
        a2dpProxy?.let { bluetoothAdapter?.closeProfileProxy(BluetoothProfile.A2DP, it) }
        headsetProxy?.let { bluetoothAdapter?.closeProfileProxy(BluetoothProfile.HEADSET, it) }
        hidProxy?.let { bluetoothAdapter?.closeProfileProxy(4, it) }
        gattMap.values.forEach {
            try {
                it.close()
            } catch (_: Exception) {}
        }
        gattMap.clear()
    }

    companion object {
        @Volatile
        private var instance: BluetoothController? = null

        fun getInstance(context: Context): BluetoothController {
            return instance ?: synchronized(this) {
                instance ?: BluetoothController(context.applicationContext).also { instance = it }
            }
        }
    }
}
