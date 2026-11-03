package com.bluetoothcontroller.app.data

import android.content.Context
import android.content.SharedPreferences

class WidgetPreferences(context: Context) {
    private val prefs: SharedPreferences =
        context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

    fun getWidgetDeviceAddresses(): List<String> {
        val raw = prefs.getString(KEY_ORDERED_DEVICES, null)
        if (!raw.isNullOrBlank()) {
            return raw.split(",").map { it.trim() }.filter { it.isNotEmpty() }
        }
        // Fallback for migration from older Set<String>
        val oldSet = prefs.getStringSet(KEY_WIDGET_DEVICES, emptySet()) ?: emptySet()
        if (oldSet.isNotEmpty()) {
            val list = oldSet.toList()
            saveWidgetDeviceAddresses(list)
            return list
        }
        return emptyList()
    }

    fun saveWidgetDeviceAddresses(addresses: List<String>) {
        val distinct = addresses.distinct()
        prefs.edit()
            .putString(KEY_ORDERED_DEVICES, distinct.joinToString(","))
            .putStringSet(KEY_WIDGET_DEVICES, distinct.toSet())
            .apply()
    }

    fun addWidgetDevice(address: String, name: String? = null) {
        val current = getWidgetDeviceAddresses().toMutableList()
        if (!current.contains(address)) {
            current.add(address)
            saveWidgetDeviceAddresses(current)
        }
        if (!name.isNullOrBlank()) {
            prefs.edit().putString(KEY_NAME_PREFIX + address, name).apply()
        }
    }

    fun removeWidgetDevice(address: String) {
        val current = getWidgetDeviceAddresses().toMutableList()
        current.remove(address)
        saveWidgetDeviceAddresses(current)
        prefs.edit().remove(KEY_NAME_PREFIX + address).apply()
    }

    fun moveUp(address: String): Boolean {
        val list = getWidgetDeviceAddresses().toMutableList()
        val index = list.indexOf(address)
        if (index > 0) {
            val item = list.removeAt(index)
            list.add(index - 1, item)
            saveWidgetDeviceAddresses(list)
            return true
        }
        return false
    }

    fun moveDown(address: String): Boolean {
        val list = getWidgetDeviceAddresses().toMutableList()
        val index = list.indexOf(address)
        if (index >= 0 && index < list.size - 1) {
            val item = list.removeAt(index)
            list.add(index + 1, item)
            saveWidgetDeviceAddresses(list)
            return true
        }
        return false
    }

    fun isDeviceInWidget(address: String): Boolean {
        return getWidgetDeviceAddresses().contains(address)
    }

    fun getDeviceName(address: String): String? {
        return prefs.getString(KEY_NAME_PREFIX + address, null)
    }

    fun saveDeviceName(address: String, name: String) {
        prefs.edit().putString(KEY_NAME_PREFIX + address, name).apply()
    }

    companion object {
        private const val PREFS_NAME = "bt_widget_prefs"
        private const val KEY_ORDERED_DEVICES = "ordered_widget_devices"
        private const val KEY_WIDGET_DEVICES = "widget_device_addresses"
        private const val KEY_NAME_PREFIX = "device_name_"
    }
}
