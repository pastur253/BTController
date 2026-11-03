package com.bluetoothcontroller.app.ui

import android.app.Activity
import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.bluetoothcontroller.app.bluetooth.BluetoothController
import com.bluetoothcontroller.app.data.BluetoothDeviceModel
import com.bluetoothcontroller.app.update.ReleaseInfo
import com.bluetoothcontroller.app.update.UpdateInstaller
import com.bluetoothcontroller.app.update.UpdateManager
import com.bluetoothcontroller.app.update.UpdateState
import com.bluetoothcontroller.app.widget.BluetoothWidgetProvider
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.io.File

class MainViewModel(application: Application) : AndroidViewModel(application) {

    private val btController = BluetoothController.getInstance(application)
    private val updateManager = UpdateManager.getInstance(application)

    private val _hasPermissions = MutableStateFlow(btController.hasPermissions())
    val hasPermissions: StateFlow<Boolean> = _hasPermissions.asStateFlow()

    val widgetDevices: StateFlow<List<BluetoothDeviceModel>> = btController.widgetDevices
    val pairedDevices: StateFlow<List<BluetoothDeviceModel>> = btController.pairedDevices
    val discoveredDevices: StateFlow<List<BluetoothDeviceModel>> = btController.discoveredDevices
    val isScanning: StateFlow<Boolean> = btController.isScanning
    val isBluetoothEnabled: StateFlow<Boolean> = btController.isBluetoothEnabled

    // Auto-update states and operations
    val updateState: StateFlow<UpdateState> = updateManager.updateState
    val currentVersionName: String = updateManager.checker.getCurrentVersionName()

    fun updatePermissionsState() {
        val granted = btController.hasPermissions()
        _hasPermissions.value = granted
        if (granted) {
            refresh()
        }
    }

    fun refresh() {
        btController.refreshDevices()
        BluetoothWidgetProvider.updateAllWidgets(getApplication())
    }

    fun toggleWidgetDevice(device: BluetoothDeviceModel) {
        if (device.isInWidget) {
            btController.removeDeviceFromWidget(device.address)
        } else {
            btController.addDeviceToWidget(device.address, device.name)
        }
        BluetoothWidgetProvider.updateAllWidgets(getApplication())
    }

    fun removeWidgetDevice(address: String) {
        btController.removeDeviceFromWidget(address)
        BluetoothWidgetProvider.updateAllWidgets(getApplication())
    }

    fun addWidgetDevice(address: String, name: String) {
        btController.addDeviceToWidget(address, name)
        BluetoothWidgetProvider.updateAllWidgets(getApplication())
    }

    fun moveWidgetDeviceUp(address: String) {
        btController.moveWidgetDeviceUp(address)
        BluetoothWidgetProvider.updateAllWidgets(getApplication())
    }

    fun moveWidgetDeviceDown(address: String) {
        btController.moveWidgetDeviceDown(address)
        BluetoothWidgetProvider.updateAllWidgets(getApplication())
    }

    fun reorderWidgetDevices(addresses: List<String>) {
        btController.reorderWidgetDevices(addresses)
        BluetoothWidgetProvider.updateAllWidgets(getApplication())
    }

    fun toggleConnection(address: String) {
        btController.toggleConnection(address)
        viewModelScope.launch {
            BluetoothWidgetProvider.updateAllWidgets(getApplication())
        }
    }

    fun startScan() {
        btController.startDiscovery()
    }

    fun stopScan() {
        btController.stopDiscovery()
    }

    fun pairDevice(address: String) {
        btController.pairDevice(address)
    }

    // Auto-update methods
    fun checkForUpdates(isManual: Boolean = false) {
        updateManager.checkForUpdates(isManual = isManual)
    }

    fun startUpdateDownload(releaseInfo: ReleaseInfo) {
        updateManager.startDownload(releaseInfo)
    }

    fun cancelUpdateDownload() {
        updateManager.cancelDownload()
    }

    fun installUpdate(apkFile: File): UpdateInstaller.InstallResult {
        return updateManager.installApk(apkFile)
    }

    fun canRequestPackageInstalls(): Boolean {
        return updateManager.canRequestPackageInstalls()
    }

    fun requestInstallPermission(activity: Activity) {
        updateManager.requestInstallPermission(activity)
    }

    fun dismissUpdate(ignoreVersion: Boolean = false) {
        updateManager.dismissUpdate(ignoreVersion)
    }

    override fun onCleared() {
        super.onCleared()
        btController.cleanup()
    }
}
