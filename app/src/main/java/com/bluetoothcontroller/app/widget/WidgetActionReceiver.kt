package com.bluetoothcontroller.app.widget

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.widget.Toast
import com.bluetoothcontroller.app.bluetooth.BluetoothController

class WidgetActionReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val btController = BluetoothController.getInstance(context)

        when (intent.action) {
            BluetoothWidgetProvider.ACTION_TOGGLE_CONNECTION -> {
                val address = intent.getStringExtra(BluetoothWidgetProvider.EXTRA_DEVICE_ADDRESS)
                if (!address.isNullOrBlank()) {
                    btController.toggleConnection(address)
                    BluetoothWidgetProvider.updateAllWidgets(context)
                }
            }

            BluetoothWidgetProvider.ACTION_REFRESH_WIDGET -> {
                btController.refreshDevices()
                BluetoothWidgetProvider.updateAllWidgets(context)
                Toast.makeText(context, "Виджет обновлен", Toast.LENGTH_SHORT).show()
            }
        }
    }
}
