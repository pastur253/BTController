package com.bluetoothcontroller.app.data

enum class DeviceType {
    HEADPHONES,
    SPEAKER,
    AUDIO_VIDEO,
    PHONE,
    COMPUTER,
    WEARABLE,
    PERIPHERAL,
    OTHER
}

data class BluetoothDeviceModel(
    val address: String,
    val name: String,
    val isConnected: Boolean = false,
    val isConnecting: Boolean = false,
    val isDisconnecting: Boolean = false,
    val isInWidget: Boolean = false,
    val deviceType: DeviceType = DeviceType.OTHER
)
