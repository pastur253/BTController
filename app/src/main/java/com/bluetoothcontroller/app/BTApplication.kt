package com.bluetoothcontroller.app

import android.app.Application
import android.util.Log
import java.lang.reflect.Method

class BTApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        instance = this
        bypassHiddenApiRestrictions()
    }

    private fun bypassHiddenApiRestrictions() {
        try {
            val forName = Class::class.java.getDeclaredMethod("forName", String::class.java)
            val getDeclaredMethod = Class::class.java.getDeclaredMethod(
                "getDeclaredMethod",
                String::class.java,
                arrayOf<Class<*>>()::class.java
            )
            val vmRuntimeClass = forName.invoke(null, "dalvik.system.VMRuntime") as Class<*>
            val getRuntime = getDeclaredMethod.invoke(vmRuntimeClass, "getRuntime", null) as Method
            val setHiddenApiExemptions = getDeclaredMethod.invoke(
                vmRuntimeClass,
                "setHiddenApiExemptions",
                arrayOf(arrayOf<String>()::class.java)
            ) as Method
            val vmRuntime = getRuntime.invoke(null)
            setHiddenApiExemptions.invoke(vmRuntime, arrayOf("L"))
            Log.d("BTApplication", "Hidden API exemptions applied successfully")
        } catch (t: Throwable) {
            Log.d("BTApplication", "Hidden API exemptions notice: ${t.message}")
        }
    }

    companion object {
        lateinit var instance: BTApplication
            private set
    }
}
