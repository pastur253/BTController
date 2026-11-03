package com.bluetoothcontroller.app.widget

import android.content.Context
import android.content.Intent
import android.graphics.Color
import android.view.View
import android.widget.RemoteViews
import android.widget.RemoteViewsService
import com.bluetoothcontroller.app.R
import com.bluetoothcontroller.app.bluetooth.BluetoothController
import com.bluetoothcontroller.app.data.BluetoothDeviceModel
import com.bluetoothcontroller.app.data.DeviceType

class BluetoothWidgetService : RemoteViewsService() {
    override fun onGetViewFactory(intent: Intent): RemoteViewsFactory {
        return BluetoothWidgetItemFactory(applicationContext)
    }
}

class BluetoothWidgetItemFactory(private val context: Context) :
    RemoteViewsService.RemoteViewsFactory {

    private var devices: List<BluetoothDeviceModel> = emptyList()
    private val btController = BluetoothController.getInstance(context)

    override fun onCreate() {
        loadDevices(forceRefresh = false)
    }

    override fun onDataSetChanged() {
        loadDevices(forceRefresh = true)
    }

    private fun loadDevices(forceRefresh: Boolean = false) {
        devices = btController.getWidgetDevicesSnapshot(forceRefresh)
    }

    override fun onDestroy() {
        devices = emptyList()
    }

    override fun getCount(): Int = devices.size

    override fun getViewAt(position: Int): RemoteViews? {
        if (position < 0 || position >= devices.size) return null
        val device = devices[position]

        val views = RemoteViews(context.packageName, R.layout.widget_device_item)
        views.setTextViewText(R.id.widget_item_name, device.name)

        // Device Icon
        val iconRes = when (device.deviceType) {
            DeviceType.HEADPHONES -> R.drawable.ic_headphones
            else -> R.drawable.ic_bluetooth
        }
        views.setImageViewResource(R.id.widget_item_icon, iconRes)

        // Status & Button state
        when {
            device.isDisconnecting -> {
                views.setTextViewText(R.id.widget_item_status, context.getString(R.string.status_disconnecting))
                views.setTextColor(R.id.widget_item_status, Color.parseColor("#FB923C"))
                views.setViewVisibility(R.id.widget_item_btn_connecting, View.VISIBLE)
                views.setViewVisibility(R.id.widget_item_btn_connect, View.GONE)
                views.setViewVisibility(R.id.widget_item_btn_disconnect, View.GONE)
            }
            device.isConnecting -> {
                views.setTextViewText(R.id.widget_item_status, context.getString(R.string.status_connecting))
                views.setTextColor(R.id.widget_item_status, Color.parseColor("#FBBF24"))
                views.setViewVisibility(R.id.widget_item_btn_connecting, View.VISIBLE)
                views.setViewVisibility(R.id.widget_item_btn_connect, View.GONE)
                views.setViewVisibility(R.id.widget_item_btn_disconnect, View.GONE)
            }
            device.isConnected -> {
                views.setTextViewText(R.id.widget_item_status, context.getString(R.string.status_connected))
                views.setTextColor(R.id.widget_item_status, Color.parseColor("#4ADE80"))
                views.setViewVisibility(R.id.widget_item_btn_disconnect, View.VISIBLE)
                views.setViewVisibility(R.id.widget_item_btn_connect, View.GONE)
                views.setViewVisibility(R.id.widget_item_btn_connecting, View.GONE)
            }
            else -> {
                views.setTextViewText(R.id.widget_item_status, context.getString(R.string.status_disconnected))
                views.setTextColor(R.id.widget_item_status, Color.parseColor("#94A3B8"))
                views.setViewVisibility(R.id.widget_item_btn_connect, View.VISIBLE)
                views.setViewVisibility(R.id.widget_item_btn_disconnect, View.GONE)
                views.setViewVisibility(R.id.widget_item_btn_connecting, View.GONE)
            }
        }

        // FillInIntent for toggle action
        val fillInIntent = Intent().apply {
            putExtra(BluetoothWidgetProvider.EXTRA_DEVICE_ADDRESS, device.address)
        }
        views.setOnClickFillInIntent(R.id.widget_item_btn_connect, fillInIntent)
        views.setOnClickFillInIntent(R.id.widget_item_btn_disconnect, fillInIntent)
        views.setOnClickFillInIntent(R.id.widget_item_btn_connecting, fillInIntent)
        views.setOnClickFillInIntent(R.id.widget_item_root, fillInIntent)

        return views
    }

    override fun getLoadingView(): RemoteViews? = null

    override fun getViewTypeCount(): Int = 1

    override fun getItemId(position: Int): Long = position.toLong()

    override fun hasStableIds(): Boolean = true
}
