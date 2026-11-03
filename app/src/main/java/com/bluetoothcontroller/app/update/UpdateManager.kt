package com.bluetoothcontroller.app.update

import android.app.Activity
import android.content.Context
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.io.File

class UpdateManager(private val context: Context) {

    val preferences = UpdatePreferences(context)
    val checker = UpdateChecker(context)
    val downloader = UpdateDownloader(context)
    val installer = UpdateInstaller(context)

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private var downloadJob: Job? = null

    private val _updateState = MutableStateFlow<UpdateState>(UpdateState.Idle)
    val updateState: StateFlow<UpdateState> = _updateState.asStateFlow()

    fun checkForUpdates(isManual: Boolean = false) {
        if (!isManual && !preferences.autoCheckEnabled) {
            return
        }

        scope.launch {
            _updateState.value = UpdateState.Checking(isManualCheck = isManual)

            when (val result = checker.checkForUpdate(forceCheck = isManual)) {
                is UpdateChecker.CheckResult.NewVersionAvailable -> {
                    _updateState.value = UpdateState.Available(
                        releaseInfo = result.releaseInfo,
                        isManualCheck = isManual
                    )
                }
                is UpdateChecker.CheckResult.UpToDate -> {
                    _updateState.value = UpdateState.NotAvailable(
                        currentVersion = result.currentVersion,
                        isManualCheck = isManual
                    )
                }
                is UpdateChecker.CheckResult.Ignored -> {
                    _updateState.value = UpdateState.Idle
                }
                is UpdateChecker.CheckResult.NoReleaseFound -> {
                    if (isManual) {
                        _updateState.value = UpdateState.Error(result.message, isManualCheck = true)
                    } else {
                        _updateState.value = UpdateState.Idle
                    }
                }
                is UpdateChecker.CheckResult.NoApkAsset -> {
                    if (isManual) {
                        _updateState.value = UpdateState.Error(result.message, isManualCheck = true)
                    } else {
                        _updateState.value = UpdateState.Idle
                    }
                }
                is UpdateChecker.CheckResult.Error -> {
                    if (isManual) {
                        _updateState.value = UpdateState.Error(result.message, isManualCheck = true)
                    } else {
                        _updateState.value = UpdateState.Idle
                    }
                }
            }
        }
    }

    fun startDownload(releaseInfo: ReleaseInfo) {
        downloadJob?.cancel()
        downloadJob = scope.launch {
            _updateState.value = UpdateState.Downloading(
                releaseInfo = releaseInfo,
                progress = 0f,
                downloadedBytes = 0L,
                totalBytes = releaseInfo.apkSize
            )

            val result = downloader.downloadApk(releaseInfo) { progress ->
                _updateState.value = UpdateState.Downloading(
                    releaseInfo = releaseInfo,
                    progress = progress.progressFraction,
                    downloadedBytes = progress.downloadedBytes,
                    totalBytes = progress.totalBytes
                )
            }

            when (result) {
                is UpdateDownloader.DownloadResult.Success -> {
                    _updateState.value = UpdateState.ReadyToInstall(
                        apkFile = result.file,
                        releaseInfo = releaseInfo
                    )
                }
                is UpdateDownloader.DownloadResult.Cancelled -> {
                    _updateState.value = UpdateState.Available(releaseInfo)
                }
                is UpdateDownloader.DownloadResult.Error -> {
                    _updateState.value = UpdateState.Error(result.message, isManualCheck = true)
                }
            }
        }
    }

    fun cancelDownload() {
        downloadJob?.cancel()
        downloadJob = null
    }

    fun installApk(file: File): UpdateInstaller.InstallResult {
        return installer.installApk(file)
    }

    fun canRequestPackageInstalls(): Boolean {
        return installer.canRequestPackageInstalls()
    }

    fun requestInstallPermission(activity: Activity) {
        installer.requestInstallPermission(activity)
    }

    fun dismissUpdate(ignoreVersion: Boolean = false) {
        val current = _updateState.value
        if (ignoreVersion && current is UpdateState.Available) {
            preferences.ignoredVersion = current.releaseInfo.versionName
        }
        cancelDownload()
        _updateState.value = UpdateState.Idle
    }

    fun clearState() {
        _updateState.value = UpdateState.Idle
    }

    companion object {
        @Volatile
        private var instance: UpdateManager? = null

        fun getInstance(context: Context): UpdateManager {
            return instance ?: synchronized(this) {
                instance ?: UpdateManager(context.applicationContext).also { instance = it }
            }
        }
    }
}
