package com.bluetoothcontroller.app.ui

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import com.bluetoothcontroller.app.R
import com.bluetoothcontroller.app.ui.theme.ErrorRed
import com.bluetoothcontroller.app.ui.theme.PrimaryBlue
import com.bluetoothcontroller.app.ui.theme.StatusConnected
import com.bluetoothcontroller.app.update.ReleaseInfo
import com.bluetoothcontroller.app.update.UpdateState
import java.io.File
import java.util.Locale

@Composable
fun UpdateDialog(
    state: UpdateState,
    currentVersion: String,
    canInstallPackages: Boolean,
    onDownload: (ReleaseInfo) -> Unit,
    onCancelDownload: () -> Unit,
    onInstall: (File) -> Unit,
    onRequestInstallPermission: () -> Unit,
    onDismiss: (ignoreVersion: Boolean) -> Unit,
    onRetry: () -> Unit
) {
    if (state is UpdateState.Idle) return

    val context = LocalContext.current

    Dialog(
        onDismissRequest = {
            if (state !is UpdateState.Downloading) {
                onDismiss(false)
            }
        },
        properties = DialogProperties(
            dismissOnBackPress = state !is UpdateState.Downloading,
            dismissOnClickOutside = state !is UpdateState.Downloading,
            usePlatformDefaultWidth = false
        )
    ) {
        Card(
            modifier = Modifier
                .fillMaxWidth(0.92f)
                .wrapContentHeight()
                .padding(16.dp),
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(
                containerColor = MaterialTheme.colorScheme.surface
            ),
            elevation = CardDefaults.cardElevation(defaultElevation = 8.dp)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(20.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                when (state) {
                    is UpdateState.Checking -> {
                        CheckingContent()
                    }
                    is UpdateState.Available -> {
                        AvailableContent(
                            releaseInfo = state.releaseInfo,
                            currentVersion = currentVersion,
                            onDownload = { onDownload(state.releaseInfo) },
                            onOpenGitHub = {
                                val browserIntent = Intent(Intent.ACTION_VIEW, Uri.parse(state.releaseInfo.htmlUrl))
                                context.startActivity(browserIntent)
                            },
                            onDismiss = { onDismiss(false) },
                            onIgnore = { onDismiss(true) }
                        )
                    }
                    is UpdateState.Downloading -> {
                        DownloadingContent(
                            state = state,
                            onCancel = onCancelDownload
                        )
                    }
                    is UpdateState.ReadyToInstall -> {
                        ReadyToInstallContent(
                            apkFile = state.apkFile,
                            releaseInfo = state.releaseInfo,
                            canInstallPackages = canInstallPackages,
                            onInstall = { onInstall(state.apkFile) },
                            onRequestPermission = onRequestInstallPermission,
                            onDismiss = { onDismiss(false) }
                        )
                    }
                    is UpdateState.NotAvailable -> {
                        UpToDateContent(
                            currentVersion = currentVersion,
                            onDismiss = { onDismiss(false) }
                        )
                    }
                    is UpdateState.Error -> {
                        ErrorContent(
                            errorMessage = state.message,
                            onRetry = onRetry,
                            onDismiss = { onDismiss(false) }
                        )
                    }
                    UpdateState.Idle -> {}
                }
            }
        }
    }
}

@Composable
private fun CheckingContent() {
    Box(
        modifier = Modifier
            .size(60.dp)
            .clip(CircleShape)
            .background(PrimaryBlue.copy(alpha = 0.12f)),
        contentAlignment = Alignment.Center
    ) {
        CircularProgressIndicator(
            modifier = Modifier.size(32.dp),
            color = PrimaryBlue,
            strokeWidth = 3.dp
        )
    }

    Spacer(modifier = Modifier.height(16.dp))

    Text(
        text = "Проверка обновлений...",
        style = MaterialTheme.typography.titleMedium,
        fontWeight = FontWeight.Bold,
        color = MaterialTheme.colorScheme.onSurface
    )

    Spacer(modifier = Modifier.height(8.dp))

    Text(
        text = "Подключение к репозиторию GitHub",
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.onSurfaceVariant
    )

    Spacer(modifier = Modifier.height(16.dp))
}

@Composable
private fun AvailableContent(
    releaseInfo: ReleaseInfo,
    currentVersion: String,
    onDownload: () -> Unit,
    onOpenGitHub: () -> Unit,
    onDismiss: () -> Unit,
    onIgnore: () -> Unit
) {
    Box(
        modifier = Modifier
            .size(56.dp)
            .clip(CircleShape)
            .background(PrimaryBlue.copy(alpha = 0.15f)),
        contentAlignment = Alignment.Center
    ) {
        Icon(
            painter = painterResource(R.drawable.ic_system_update),
            contentDescription = null,
            tint = PrimaryBlue,
            modifier = Modifier.size(30.dp)
        )
    }

    Spacer(modifier = Modifier.height(12.dp))

    Text(
        text = "Доступно обновление!",
        style = MaterialTheme.typography.titleLarge,
        fontWeight = FontWeight.Bold,
        color = MaterialTheme.colorScheme.onSurface
    )

    Spacer(modifier = Modifier.height(6.dp))

    // Version badge
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.Center
    ) {
        Surface(
            shape = RoundedCornerShape(8.dp),
            color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.6f)
        ) {
            Text(
                text = "v$currentVersion  ➔  v${releaseInfo.versionName}",
                fontSize = 13.sp,
                fontWeight = FontWeight.SemiBold,
                color = PrimaryBlue,
                modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp)
            )
        }
    }

    if (releaseInfo.apkSize > 0) {
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            text = "Размер: ${formatFileSize(releaseInfo.apkSize)}",
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }

    Spacer(modifier = Modifier.height(12.dp))

    // Changelog card
    if (releaseInfo.releaseNotes.isNotBlank()) {
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(max = 160.dp),
            shape = RoundedCornerShape(12.dp),
            colors = CardDefaults.cardColors(
                containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.35f)
            )
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(12.dp)
                    .verticalScroll(rememberScrollState())
            ) {
                Text(
                    text = "Что нового:",
                    fontWeight = FontWeight.Bold,
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = releaseInfo.releaseNotes,
                    fontSize = 13.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    lineHeight = 18.sp
                )
            }
        }
        Spacer(modifier = Modifier.height(16.dp))
    }

    // Action buttons
    Button(
        onClick = onDownload,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = ButtonDefaults.buttonColors(containerColor = PrimaryBlue)
    ) {
        Icon(
            painter = painterResource(R.drawable.ic_download),
            contentDescription = null,
            modifier = Modifier.size(18.dp)
        )
        Spacer(modifier = Modifier.width(8.dp))
        Text(text = "Скачать и установить", fontWeight = FontWeight.Bold)
    }

    Spacer(modifier = Modifier.height(8.dp))

    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        TextButton(onClick = onOpenGitHub) {
            Text(text = "GitHub", fontSize = 13.sp, color = PrimaryBlue)
        }
        TextButton(onClick = onIgnore) {
            Text(text = "Пропустить", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        TextButton(onClick = onDismiss) {
            Text(text = "Позже", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun DownloadingContent(
    state: UpdateState.Downloading,
    onCancel: () -> Unit
) {
    Box(
        modifier = Modifier
            .size(56.dp)
            .clip(CircleShape)
            .background(PrimaryBlue.copy(alpha = 0.15f)),
        contentAlignment = Alignment.Center
    ) {
        Icon(
            painter = painterResource(R.drawable.ic_download),
            contentDescription = null,
            tint = PrimaryBlue,
            modifier = Modifier.size(30.dp)
        )
    }

    Spacer(modifier = Modifier.height(14.dp))

    Text(
        text = "Загрузка обновления...",
        style = MaterialTheme.typography.titleLarge,
        fontWeight = FontWeight.Bold,
        color = MaterialTheme.colorScheme.onSurface
    )

    Spacer(modifier = Modifier.height(6.dp))

    Text(
        text = "Версия ${state.releaseInfo.versionName}",
        fontSize = 13.sp,
        color = MaterialTheme.colorScheme.onSurfaceVariant
    )

    Spacer(modifier = Modifier.height(16.dp))

    LinearProgressIndicator(
        progress = { state.progress },
        modifier = Modifier
            .fillMaxWidth()
            .height(8.dp)
            .clip(RoundedCornerShape(4.dp)),
        color = PrimaryBlue,
        trackColor = MaterialTheme.colorScheme.surfaceVariant
    )

    Spacer(modifier = Modifier.height(8.dp))

    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(
            text = "${(state.progress * 100).toInt()}%",
            fontSize = 12.sp,
            fontWeight = FontWeight.Bold,
            color = PrimaryBlue
        )
        Text(
            text = if (state.totalBytes > 0) {
                "${formatFileSize(state.downloadedBytes)} / ${formatFileSize(state.totalBytes)}"
            } else {
                formatFileSize(state.downloadedBytes)
            },
            fontSize = 12.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }

    Spacer(modifier = Modifier.height(18.dp))

    OutlinedButton(
        onClick = onCancel,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp)
    ) {
        Text(text = "Отмена", color = ErrorRed)
    }
}

@Composable
private fun ReadyToInstallContent(
    apkFile: File,
    releaseInfo: ReleaseInfo,
    canInstallPackages: Boolean,
    onInstall: () -> Unit,
    onRequestPermission: () -> Unit,
    onDismiss: () -> Unit
) {
    Box(
        modifier = Modifier
            .size(56.dp)
            .clip(CircleShape)
            .background(StatusConnected.copy(alpha = 0.15f)),
        contentAlignment = Alignment.Center
    ) {
        Icon(
            painter = painterResource(R.drawable.ic_system_update),
            contentDescription = null,
            tint = StatusConnected,
            modifier = Modifier.size(30.dp)
        )
    }

    Spacer(modifier = Modifier.height(14.dp))

    Text(
        text = "Готово к установке!",
        style = MaterialTheme.typography.titleLarge,
        fontWeight = FontWeight.Bold,
        color = MaterialTheme.colorScheme.onSurface
    )

    Spacer(modifier = Modifier.height(6.dp))

    Text(
        text = "Версия ${releaseInfo.versionName} скачана",
        fontSize = 13.sp,
        color = MaterialTheme.colorScheme.onSurfaceVariant
    )

    Spacer(modifier = Modifier.height(16.dp))

    if (!canInstallPackages) {
        Surface(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(12.dp),
            color = ErrorRed.copy(alpha = 0.1f)
        ) {
            Column(modifier = Modifier.padding(12.dp)) {
                Text(
                    text = "Требуется разрешение",
                    fontWeight = FontWeight.Bold,
                    fontSize = 13.sp,
                    color = ErrorRed
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = "Для установки обновления разрешите приложению установку из этого источника в настройках Android.",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Spacer(modifier = Modifier.height(8.dp))
                Button(
                    onClick = onRequestPermission,
                    colors = ButtonDefaults.buttonColors(containerColor = ErrorRed),
                    shape = RoundedCornerShape(8.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(text = "Разрешить установку", fontSize = 12.sp)
                }
            }
        }
        Spacer(modifier = Modifier.height(12.dp))
    }

    Button(
        onClick = onInstall,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = ButtonDefaults.buttonColors(containerColor = StatusConnected)
    ) {
        Icon(
            painter = painterResource(R.drawable.ic_system_update),
            contentDescription = null,
            modifier = Modifier.size(18.dp)
        )
        Spacer(modifier = Modifier.width(8.dp))
        Text(text = "Установить обновление", fontWeight = FontWeight.Bold)
    }

    Spacer(modifier = Modifier.height(8.dp))

    TextButton(
        onClick = onDismiss,
        modifier = Modifier.fillMaxWidth()
    ) {
        Text(text = "Закрыть", color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun UpToDateContent(
    currentVersion: String,
    onDismiss: () -> Unit
) {
    Box(
        modifier = Modifier
            .size(56.dp)
            .clip(CircleShape)
            .background(StatusConnected.copy(alpha = 0.15f)),
        contentAlignment = Alignment.Center
    ) {
        Icon(
            painter = painterResource(R.drawable.ic_system_update),
            contentDescription = null,
            tint = StatusConnected,
            modifier = Modifier.size(30.dp)
        )
    }

    Spacer(modifier = Modifier.height(14.dp))

    Text(
        text = "У вас последняя версия",
        style = MaterialTheme.typography.titleLarge,
        fontWeight = FontWeight.Bold,
        color = MaterialTheme.colorScheme.onSurface
    )

    Spacer(modifier = Modifier.height(6.dp))

    Text(
        text = "BT Controller v$currentVersion — самая свежая версия. Обновлений не требуется.",
        fontSize = 13.sp,
        textAlign = TextAlign.Center,
        color = MaterialTheme.colorScheme.onSurfaceVariant
    )

    Spacer(modifier = Modifier.height(20.dp))

    Button(
        onClick = onDismiss,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = ButtonDefaults.buttonColors(containerColor = PrimaryBlue)
    ) {
        Text(text = "Отлично")
    }
}

@Composable
private fun ErrorContent(
    errorMessage: String,
    onRetry: () -> Unit,
    onDismiss: () -> Unit
) {
    Box(
        modifier = Modifier
            .size(56.dp)
            .clip(CircleShape)
            .background(ErrorRed.copy(alpha = 0.15f)),
        contentAlignment = Alignment.Center
    ) {
        Icon(
            painter = painterResource(R.drawable.ic_stop),
            contentDescription = null,
            tint = ErrorRed,
            modifier = Modifier.size(30.dp)
        )
    }

    Spacer(modifier = Modifier.height(14.dp))

    Text(
        text = "Ошибка обновления",
        style = MaterialTheme.typography.titleLarge,
        fontWeight = FontWeight.Bold,
        color = MaterialTheme.colorScheme.onSurface
    )

    Spacer(modifier = Modifier.height(6.dp))

    Text(
        text = errorMessage,
        fontSize = 13.sp,
        textAlign = TextAlign.Center,
        color = MaterialTheme.colorScheme.onSurfaceVariant
    )

    Spacer(modifier = Modifier.height(20.dp))

    Button(
        onClick = onRetry,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = ButtonDefaults.buttonColors(containerColor = PrimaryBlue)
    ) {
        Text(text = "Повторить попытку")
    }

    Spacer(modifier = Modifier.height(8.dp))

    TextButton(
        onClick = onDismiss,
        modifier = Modifier.fillMaxWidth()
    ) {
        Text(text = "Закрыть", color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

private fun formatFileSize(bytes: Long): String {
    if (bytes <= 0) return "0 Б"
    val kb = bytes / 1024.0
    val mb = kb / 1024.0
    val gb = mb / 1024.0

    return when {
        gb >= 1.0 -> String.format(Locale.getDefault(), "%.1f ГБ", gb)
        mb >= 1.0 -> String.format(Locale.getDefault(), "%.1f МБ", mb)
        kb >= 1.0 -> String.format(Locale.getDefault(), "%.0f КБ", kb)
        else -> "$bytes Б"
    }
}
