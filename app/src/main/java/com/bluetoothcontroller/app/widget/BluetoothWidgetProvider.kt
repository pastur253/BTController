package com.bluetoothcontroller.app.widget

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.widget.RemoteViews
import com.bluetoothcontroller.app.R
import com.bluetoothcontroller.app.ui.MainActivity

class BluetoothWidgetProvider : AppWidgetProvider() {

    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray
    ) {
        for (appWidgetId in appWidgetIds) {
            updateAppWidget(context, appWidgetManager, appWidgetId)
        }
        super.onUpdate(context, appWidgetManager, appWidgetIds)
    }

    companion object {
        const val ACTION_TOGGLE_CONNECTION = "com.bluetoothcontroller.app.ACTION_TOGGLE_CONNECTION"
        const val ACTION_REFRESH_WIDGET = "com.bluetoothcontroller.app.ACTION_REFRESH_WIDGET"
        const val EXTRA_DEVICE_ADDRESS = "extra_device_address"

        fun updateAppWidget(
            context: Context,
            appWidgetManager: AppWidgetManager,
            appWidgetId: Int
        ) {
            val remoteViews = RemoteViews(context.packageName, R.layout.widget_layout)

            // Intent to launch Main Activity from Settings button or empty view
            val settingsIntent = Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
            }
            val settingsPendingIntent = PendingIntent.getActivity(
                context,
                100,
                settingsIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            remoteViews.setOnClickPendingIntent(R.id.widget_btn_settings, settingsPendingIntent)
            remoteViews.setOnClickPendingIntent(R.id.widget_empty_view, settingsPendingIntent)

            // Refresh action intent
            val refreshIntent = Intent(context, WidgetActionReceiver::class.java).apply {
                action = ACTION_REFRESH_WIDGET
            }
            val refreshPendingIntent = PendingIntent.getBroadcast(
                context,
                101,
                refreshIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            remoteViews.setOnClickPendingIntent(R.id.widget_btn_refresh, refreshPendingIntent)

            // Service Intent for ListView
            val serviceIntent = Intent(context, BluetoothWidgetService::class.java).apply {
                putExtra(AppWidgetManager.EXTRA_APPWIDGET_ID, appWidgetId)
                data = Uri.fromParts("content", "widget_$appWidgetId", null)
            }
            remoteViews.setRemoteAdapter(R.id.widget_device_list, serviceIntent)
            remoteViews.setEmptyView(R.id.widget_device_list, R.id.widget_empty_view)

            // PendingIntent template for item clicks (Connect/Disconnect toggle)
            val toggleIntent = Intent(context, WidgetActionReceiver::class.java).apply {
                action = ACTION_TOGGLE_CONNECTION
            }
            val togglePendingIntent = PendingIntent.getBroadcast(
                context,
                102,
                toggleIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_MUTABLE
            )
            remoteViews.setPendingIntentTemplate(R.id.widget_device_list, togglePendingIntent)

            appWidgetManager.updateAppWidget(appWidgetId, remoteViews)
            appWidgetManager.notifyAppWidgetViewDataChanged(appWidgetId, R.id.widget_device_list)
        }

        fun updateAllWidgets(context: Context) {
            val appWidgetManager = AppWidgetManager.getInstance(context)
            val widgetIds = appWidgetManager.getAppWidgetIds(
                ComponentName(context, BluetoothWidgetProvider::class.java)
            )
            if (widgetIds.isNotEmpty()) {
                for (id in widgetIds) {
                    updateAppWidget(context, appWidgetManager, id)
                }
                appWidgetManager.notifyAppWidgetViewDataChanged(widgetIds, R.id.widget_device_list)
            }
        }
    }
}
