package com.bluetoothcontroller.app.bluetooth

import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothDevice
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Build

class BluetoothConnectionReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val btController = BluetoothController.getInstance(context)

        val device: BluetoothDevice? = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE, BluetoothDevice::class.java)
        } else {
            @Suppress("DEPRECATION")
            intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE)
        }
        val address = device?.address

        when (intent.action) {
            BluetoothDevice.ACTION_ACL_DISCONNECTED,
            BluetoothDevice.ACTION_ACL_CONNECTED -> {
                address?.let { btController.clearTransitionState(it) }
                btController.refreshDevices()
            }
            BluetoothDevice.ACTION_NAME_CHANGED,
            "android.bluetooth.device.action.ALIAS_CHANGED" -> {
                val extraName = intent.getStringExtra(BluetoothDevice.EXTRA_NAME)
                if (!address.isNullOrBlank() && !extraName.isNullOrBlank()) {
                    com.bluetoothcontroller.app.data.WidgetPreferences(context).saveDeviceName(address, extraName)
                }
                btController.refreshDevices()
            }
            BluetoothDevice.ACTION_BOND_STATE_CHANGED,
            BluetoothAdapter.ACTION_STATE_CHANGED,
            BluetoothAdapter.ACTION_CONNECTION_STATE_CHANGED,
            BluetoothAdapter.ACTION_LOCAL_NAME_CHANGED,
            "android.bluetooth.a2dp.profile.action.CONNECTION_STATE_CHANGED",
            "android.bluetooth.headset.profile.action.CONNECTION_STATE_CHANGED",
            Intent.ACTION_BOOT_COMPLETED -> {
                btController.refreshDevices()
            }
        }
    }
}
