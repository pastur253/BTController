package com.bluetoothcontroller.app.update

import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.io.BufferedReader
import java.io.InputStreamReader
import java.net.HttpURLConnection
import java.net.URL

class UpdateChecker(private val context: Context) {

    private val preferences = UpdatePreferences(context)

    suspend fun checkForUpdate(
        owner: String = preferences.repositoryOwner,
        repo: String = preferences.repositoryName,
        forceCheck: Boolean = false
    ): CheckResult = withContext(Dispatchers.IO) {
        val currentVersion = getCurrentVersionName()

        try {
            val apiUrl = "https://api.github.com/repos/$owner/$repo/releases/latest"
            val url = URL(apiUrl)
            val connection = (url.openConnection() as HttpURLConnection).apply {
                requestMethod = "GET"
                connectTimeout = 10_000
                readTimeout = 15_000
                setRequestProperty("Accept", "application/vnd.github.v3+json")
                setRequestProperty("User-Agent", "BTController-Android/$currentVersion")
            }

            val responseCode = connection.responseCode
            if (responseCode == HttpURLConnection.HTTP_NOT_FOUND) {
                return@withContext CheckResult.NoReleaseFound("Релизы в репозитории $owner/$repo не найдены")
            }

            if (responseCode != HttpURLConnection.HTTP_OK) {
                val errorStream = connection.errorStream
                val errorMsg = errorStream?.bufferedReader()?.use { it.readText() } ?: ""
                Log.w(TAG, "GitHub API returned HTTP $responseCode: $errorMsg")
                return@withContext CheckResult.Error("Ошибка сервера GitHub: HTTP $responseCode")
            }

            val responseBody = BufferedReader(InputStreamReader(connection.inputStream)).use { it.readText() }
            val json = JSONObject(responseBody)

            val tagName = json.optString("tag_name", "").trim()
            val releaseTitle = json.optString("name", tagName).ifBlank { tagName }
            val releaseNotes = json.optString("body", "").trim()
            val publishedAt = json.optString("published_at", "")
            val htmlUrl = json.optString("html_url", "https://github.com/$owner/$repo/releases")

            // Parse assets to find APK
            val assetsArray = json.optJSONArray("assets")
            if (assetsArray == null || assetsArray.length() == 0) {
                return@withContext CheckResult.NoApkAsset(
                    "В релизе $tagName нет прикрепленных APK-файлов"
                )
            }

            // Find best matching APK: prefer release APK over debug, but take any APK if only one
            var selectedAsset: JSONObject? = null
            var bestScore = -1

            for (i in 0 until assetsArray.length()) {
                val asset = assetsArray.getJSONObject(i)
                val assetName = asset.optString("name", "").lowercase()
                if (assetName.endsWith(".apk")) {
                    var score = 1
                    if (assetName.contains("release")) score += 2
                    if (!assetName.contains("debug")) score += 1

                    if (score > bestScore) {
                        bestScore = score
                        selectedAsset = asset
                    }
                }
            }

            if (selectedAsset == null) {
                return@withContext CheckResult.NoApkAsset(
                    "В последнем релизе $tagName не найден файл установки (.apk)"
                )
            }

            val downloadUrl = selectedAsset.getString("browser_download_url")
            val apkName = selectedAsset.optString("name", "BTController-$tagName.apk")
            val apkSize = selectedAsset.optLong("size", 0L)

            val cleanRemoteVersion = cleanVersion(tagName)
            val cleanCurrentVersion = cleanVersion(currentVersion)

            val isNewer = compareVersions(cleanRemoteVersion, cleanCurrentVersion) > 0

            // Record last check time
            preferences.lastCheckTime = System.currentTimeMillis()

            val releaseInfo = ReleaseInfo(
                versionName = cleanRemoteVersion,
                releaseTitle = releaseTitle,
                releaseNotes = releaseNotes,
                publishedAt = publishedAt,
                downloadUrl = downloadUrl,
                apkFileName = apkName,
                apkSize = apkSize,
                htmlUrl = htmlUrl
            )

            if (!isNewer) {
                return@withContext CheckResult.UpToDate(currentVersion)
            }

            // If not forced (auto-check) and user chose to ignore this version, skip
            if (!forceCheck && preferences.ignoredVersion == cleanRemoteVersion) {
                return@withContext CheckResult.Ignored(releaseInfo)
            }

            CheckResult.NewVersionAvailable(releaseInfo)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to check for updates", e)
            CheckResult.Error(e.localizedMessage ?: "Не удалось проверить обновления")
        }
    }

    fun getCurrentVersionName(): String {
        return try {
            val packageInfo = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                context.packageManager.getPackageInfo(
                    context.packageName,
                    PackageManager.PackageInfoFlags.of(0)
                )
            } else {
                @Suppress("DEPRECATION")
                context.packageManager.getPackageInfo(context.packageName, 0)
            }
            packageInfo.versionName ?: "1.0.0"
        } catch (e: Exception) {
            "1.0.0"
        }
    }

    companion object {
        private const val TAG = "UpdateChecker"

        fun cleanVersion(raw: String): String {
            return raw.trim()
                .removePrefix("v")
                .removePrefix("V")
                .trim()
        }

        /**
         * Compares two semantic version strings.
         * Returns > 0 if v1 > v2
         * Returns < 0 if v1 < v2
         * Returns 0 if equal
         */
        fun compareVersions(v1: String, v2: String): Int {
            // Split off any metadata or pre-release suffixes (e.g. 1.0.0-beta -> 1.0.0)
            val v1Base = v1.substringBefore("-").substringBefore("+")
            val v2Base = v2.substringBefore("-").substringBefore("+")

            val parts1 = v1Base.split(".").mapNotNull { it.toIntOrNull() }
            val parts2 = v2Base.split(".").mapNotNull { it.toIntOrNull() }

            val maxLen = maxOf(parts1.size, parts2.size)
            for (i in 0 until maxLen) {
                val num1 = parts1.getOrElse(i) { 0 }
                val num2 = parts2.getOrElse(i) { 0 }
                if (num1 != num2) {
                    return num1.compareTo(num2)
                }
            }

            // If base numbers are equal, check pre-release suffix:
            // "1.0.0" without suffix is newer than "1.0.0-beta"
            val hasSuffix1 = v1.contains("-")
            val hasSuffix2 = v2.contains("-")
            return when {
                !hasSuffix1 && hasSuffix2 -> 1
                hasSuffix1 && !hasSuffix2 -> -1
                else -> 0
            }
        }
    }

    sealed interface CheckResult {
        data class NewVersionAvailable(val releaseInfo: ReleaseInfo) : CheckResult
        data class UpToDate(val currentVersion: String) : CheckResult
        data class Ignored(val releaseInfo: ReleaseInfo) : CheckResult
        data class NoReleaseFound(val message: String) : CheckResult
        data class NoApkAsset(val message: String) : CheckResult
        data class Error(val message: String) : CheckResult
    }
}
