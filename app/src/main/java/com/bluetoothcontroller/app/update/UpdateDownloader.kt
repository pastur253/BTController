package com.bluetoothcontroller.app.update

import android.content.Context
import android.os.Environment
import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.isActive
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileOutputStream
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URL

class UpdateDownloader(private val context: Context) {

    data class Progress(
        val downloadedBytes: Long,
        val totalBytes: Long,
        val progressFraction: Float
    )

    suspend fun downloadApk(
        releaseInfo: ReleaseInfo,
        onProgress: (Progress) -> Unit
    ): DownloadResult = withContext(Dispatchers.IO) {
        val targetDir = context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS)
            ?: File(context.cacheDir, "updates").apply { mkdirs() }

        if (!targetDir.exists()) {
            targetDir.mkdirs()
        }

        // Clean name
        val safeFileName = releaseInfo.apkFileName.ifBlank {
            "BTController-${releaseInfo.versionName}.apk"
        }
        val targetFile = File(targetDir, safeFileName)
        val tempFile = File(targetDir, "$safeFileName.download")

        // If complete file already exists with matching size, reuse it!
        if (targetFile.exists() && releaseInfo.apkSize > 0 && targetFile.length() == releaseInfo.apkSize) {
            Log.d(TAG, "File already downloaded: ${targetFile.absolutePath}")
            onProgress(Progress(releaseInfo.apkSize, releaseInfo.apkSize, 1.0f))
            return@withContext DownloadResult.Success(targetFile)
        }

        var connection: HttpURLConnection? = null
        var inputStream: InputStream? = null
        var outputStream: FileOutputStream? = null

        try {
            var currentUrl = releaseInfo.downloadUrl
            var redirectsCount = 0
            val maxRedirects = 10

            // Follow HTTP redirects manually (Github Release Assets redirect to S3/Azure with 302)
            while (redirectsCount < maxRedirects) {
                val url = URL(currentUrl)
                connection = (url.openConnection() as HttpURLConnection).apply {
                    instanceFollowRedirects = false
                    connectTimeout = 15_000
                    readTimeout = 30_000
                    setRequestProperty("User-Agent", "BTController-Android")
                }

                val status = connection.responseCode
                if (status == HttpURLConnection.HTTP_MOVED_TEMP ||
                    status == HttpURLConnection.HTTP_MOVED_PERM ||
                    status == HttpURLConnection.HTTP_SEE_OTHER ||
                    status == 307 || status == 308
                ) {
                    val newUrl = connection.getHeaderField("Location")
                    connection.disconnect()
                    if (newUrl.isNullOrEmpty()) {
                        throw IllegalStateException("Редирект без заголовка Location")
                    }
                    currentUrl = newUrl
                    redirectsCount++
                } else if (status == HttpURLConnection.HTTP_OK) {
                    break
                } else {
                    throw IllegalStateException("Ошибка сервера: HTTP $status")
                }
            }

            val finalConnection = connection ?: throw IllegalStateException("Не удалось установить соединение")
            val totalBytes = if (releaseInfo.apkSize > 0) {
                releaseInfo.apkSize
            } else {
                finalConnection.contentLength.toLong()
            }

            inputStream = finalConnection.inputStream
            outputStream = FileOutputStream(tempFile)

            val buffer = ByteArray(8 * 1024)
            var downloadedBytes = 0L
            var bytesRead: Int

            while (inputStream.read(buffer).also { bytesRead = it } != -1) {
                if (!coroutineContext.isActive) {
                    tempFile.delete()
                    return@withContext DownloadResult.Cancelled
                }

                outputStream.write(buffer, 0, bytesRead)
                downloadedBytes += bytesRead

                val fraction = if (totalBytes > 0) {
                    (downloadedBytes.toFloat() / totalBytes).coerceIn(0f, 1f)
                } else {
                    0f
                }
                onProgress(Progress(downloadedBytes, totalBytes, fraction))
            }

            outputStream.flush()
            outputStream.close()
            outputStream = null

            // Rename temp to target
            if (targetFile.exists()) {
                targetFile.delete()
            }
            if (!tempFile.renameTo(targetFile)) {
                tempFile.copyTo(targetFile, overwrite = true)
                tempFile.delete()
            }

            Log.d(TAG, "Download finished successfully: ${targetFile.absolutePath}")
            DownloadResult.Success(targetFile)
        } catch (e: Exception) {
            Log.e(TAG, "Download error", e)
            tempFile.delete()
            if (!coroutineContext.isActive) {
                DownloadResult.Cancelled
            } else {
                DownloadResult.Error(e.localizedMessage ?: "Ошибка скачивания файла обновления")
            }
        } finally {
            try {
                outputStream?.close()
                inputStream?.close()
                connection?.disconnect()
            } catch (_: Exception) {}
        }
    }

    sealed interface DownloadResult {
        data class Success(val file: File) : DownloadResult
        data class Error(val message: String) : DownloadResult
        object Cancelled : DownloadResult
    }

    companion object {
        private const val TAG = "UpdateDownloader"
    }
}
