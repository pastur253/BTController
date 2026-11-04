package com.bluetoothcontroller.app.ui

import android.Manifest
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.zIndex
import com.bluetoothcontroller.app.R
import com.bluetoothcontroller.app.data.BluetoothDeviceModel
import com.bluetoothcontroller.app.data.DeviceType
import com.bluetoothcontroller.app.ui.theme.*
import com.bluetoothcontroller.app.update.UpdateInstaller
import com.bluetoothcontroller.app.update.UpdateState

class MainActivity : ComponentActivity() {

    private val viewModel: MainViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        setContent {
            BTControllerTheme {
                MainScreen(
                    viewModel = viewModel,
                    onRequestPermissions = { requestPermissionsLauncher ->
                        val permissions = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                            arrayOf(
                                Manifest.permission.BLUETOOTH_CONNECT,
                                Manifest.permission.BLUETOOTH_SCAN
                            )
                        } else {
                            arrayOf(
                                Manifest.permission.BLUETOOTH,
                                Manifest.permission.BLUETOOTH_ADMIN,
                                Manifest.permission.ACCESS_FINE_LOCATION
                            )
                        }
                        requestPermissionsLauncher.launch(permissions)
                    },
                    onOpenBluetoothSettings = {
                        startActivity(Intent(Settings.ACTION_BLUETOOTH_SETTINGS))
                    }
                )
            }
        }
    }

    override fun onResume() {
        super.onResume()
        viewModel.updatePermissionsState()
        viewModel.refresh()
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScreen(
    viewModel: MainViewModel,
    onRequestPermissions: (androidx.activity.result.ActivityResultLauncher<Array<String>>) -> Unit,
    onOpenBluetoothSettings: () -> Unit
) {
    val hasPermissions by viewModel.hasPermissions.collectAsState()
    val isBtEnabled by viewModel.isBluetoothEnabled.collectAsState()
    val isScanning by viewModel.isScanning.collectAsState()
    val widgetDevices by viewModel.widgetDevices.collectAsState()
    val pairedDevices by viewModel.pairedDevices.collectAsState()
    val discoveredDevices by viewModel.discoveredDevices.collectAsState()
    val updateState by viewModel.updateState.collectAsState()

    var showAboutDialog by remember { mutableStateOf(false) }

    val context = LocalContext.current

    val installPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.StartActivityForResult()
    ) {
        // Returned from system settings for unknown app install permission
    }

    var localWidgetDevices by remember { mutableStateOf(widgetDevices) }
    var draggingAddress by remember { mutableStateOf<String?>(null) }
    var dragOffsetY by remember { mutableFloatStateOf(0f) }

    val density = LocalDensity.current
    val itemHeightPx = with(density) { 76.dp.toPx() }

    LaunchedEffect(widgetDevices) {
        if (draggingAddress == null) {
            localWidgetDevices = widgetDevices
        }
    }

    val onDragStart: (String) -> Unit = { addr ->
        draggingAddress = addr
        dragOffsetY = 0f
    }

    val onDrag: (Float) -> Unit = { deltaY ->
        val addr = draggingAddress
        if (addr != null) {
            dragOffsetY += deltaY
            val currentIndex = localWidgetDevices.indexOfFirst { it.address == addr }
            if (currentIndex != -1) {
                val threshold = itemHeightPx * 0.6f
                if (dragOffsetY > threshold && currentIndex < localWidgetDevices.size - 1) {
                    val updated = localWidgetDevices.toMutableList()
                    val item = updated.removeAt(currentIndex)
                    updated.add(currentIndex + 1, item)
                    localWidgetDevices = updated
                    dragOffsetY -= itemHeightPx
                } else if (dragOffsetY < -threshold && currentIndex > 0) {
                    val updated = localWidgetDevices.toMutableList()
                    val item = updated.removeAt(currentIndex)
                    updated.add(currentIndex - 1, item)
                    localWidgetDevices = updated
                    dragOffsetY += itemHeightPx
                }
            }
        }
    }

    val onDragEnd: () -> Unit = {
        if (draggingAddress != null) {
            viewModel.reorderWidgetDevices(localWidgetDevices.map { it.address })
            draggingAddress = null
            dragOffsetY = 0f
        }
    }

    val onDragCancel: () -> Unit = {
        draggingAddress = null
        dragOffsetY = 0f
        localWidgetDevices = widgetDevices
    }

    val permissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestMultiplePermissions()
    ) {
        viewModel.updatePermissionsState()
    }

    LaunchedEffect(Unit) {
        if (!hasPermissions) {
            onRequestPermissions(permissionLauncher)
        }
        // Auto-check for updates from remote repository on launch
        viewModel.checkForUpdates(isManual = false)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            painter = painterResource(R.drawable.ic_bluetooth),
                            contentDescription = null,
                            tint = PrimaryBlue,
                            modifier = Modifier.size(28.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = "BT Controller",
                            fontWeight = FontWeight.Bold,
                            fontSize = 20.sp
                        )
                    }
                },
                actions = {
                    IconButton(
                        onClick = {
                            if (isScanning) viewModel.stopScan() else viewModel.startScan()
                        }
                    ) {
                        Icon(
                            painter = painterResource(if (isScanning) R.drawable.ic_stop else R.drawable.ic_search),
                            contentDescription = if (isScanning) "Остановить поиск" else "Поиск устройств",
                            tint = if (isScanning) ErrorRed else PrimaryBlue,
                            modifier = Modifier.size(24.dp)
                        )
                    }
                    IconButton(onClick = { viewModel.refresh() }) {
                        Icon(
                            painter = painterResource(R.drawable.ic_refresh),
                            contentDescription = "Обновить список",
                            tint = PrimaryBlue,
                            modifier = Modifier.size(24.dp)
                        )
                    }
                    IconButton(onClick = { showAboutDialog = true }) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                painter = painterResource(R.drawable.ic_info),
                                contentDescription = "О приложении",
                                tint = PrimaryBlue,
                                modifier = Modifier.size(24.dp)
                            )
                            if (updateState is UpdateState.Available) {
                                Box(
                                    modifier = Modifier
                                        .size(8.dp)
                                        .clip(CircleShape)
                                        .background(ErrorRed)
                                        .align(Alignment.TopEnd)
                                )
                            }
                        }
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface
                )
            )
        }
    ) { paddingValues ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(horizontal = 16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
            contentPadding = PaddingValues(vertical = 16.dp)
        ) {
            // Permission Notice
            if (!hasPermissions) {
                item {
                    PermissionCard(
                        onRequest = { onRequestPermissions(permissionLauncher) }
                    )
                }
            }

            // Bluetooth Disabled Notice
            if (hasPermissions && !isBtEnabled) {
                item {
                    BluetoothDisabledCard(onEnable = onOpenBluetoothSettings)
                }
            }

            // SECTION 1: WIDGET DEVICES
            item {
                SectionHeader(
                    title = "Устройства в виджете (${localWidgetDevices.size})",
                    subtitle = "Перетаскивайте за иконку для изменения порядка"
                )
            }

            if (localWidgetDevices.isEmpty()) {
                item {
                    EmptyWidgetDevicesCard()
                }
            } else {
                itemsIndexed(localWidgetDevices, key = { _, it -> "widget_${it.address}" }) { index, device ->
                    val isDragging = (device.address == draggingAddress)
                    DeviceCard(
                        device = device,
                        isWidgetDevice = true,
                        orderIndex = index + 1,
                        isDragging = isDragging,
                        canMoveUp = index > 0,
                        canMoveDown = index < localWidgetDevices.size - 1,
                        onMoveUp = { viewModel.moveWidgetDeviceUp(device.address) },
                        onMoveDown = { viewModel.moveWidgetDeviceDown(device.address) },
                        onDragStart = onDragStart,
                        onDrag = onDrag,
                        onDragEnd = onDragEnd,
                        onDragCancel = onDragCancel,
                        onToggleConnection = { viewModel.toggleConnection(device.address) },
                        onToggleWidget = { viewModel.removeWidgetDevice(device.address) },
                        modifier = Modifier
                            .animateItem()
                            .zIndex(if (isDragging) 10f else 1f)
                            .graphicsLayer {
                                translationY = if (isDragging) dragOffsetY else 0f
                                scaleX = if (isDragging) 1.03f else 1f
                                scaleY = if (isDragging) 1.03f else 1f
                            }
                    )
                }
            }

            // SECTION 2: PAIRED DEVICES
            item {
                Spacer(modifier = Modifier.height(8.dp))
                SectionHeader(
                    title = "Сопряженные устройства (${pairedDevices.size})",
                    subtitle = "Нажмите +, чтобы добавить устройство в виджет"
                )
            }

            if (pairedDevices.isEmpty()) {
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                    ) {
                        Text(
                            text = "Нет сопряженных устройств",
                            modifier = Modifier.padding(16.dp),
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            } else {
                items(pairedDevices, key = { "paired_${it.address}" }) { device ->
                    DeviceCard(
                        device = device,
                        isWidgetDevice = false,
                        onToggleConnection = { viewModel.toggleConnection(device.address) },
                        onToggleWidget = { viewModel.addWidgetDevice(device.address, device.name) }
                    )
                }
            }

            // SECTION 3: DISCOVERED DEVICES
            if (discoveredDevices.isNotEmpty() || isScanning) {
                item {
                    Spacer(modifier = Modifier.height(8.dp))
                    SectionHeader(
                        title = "Найденные устройства (${discoveredDevices.size})",
                        subtitle = if (isScanning) "Идет сканирование..." else "Нажмите для сопряжения"
                    )
                }

                items(discoveredDevices, key = { "discovered_${it.address}" }) { device ->
                    DiscoveredDeviceCard(
                        device = device,
                        onPair = { viewModel.pairDevice(device.address) }
                    )
                }
            }

            // SECTION 4: APP INFO
            item {
                AppVersionCard(
                    versionName = viewModel.currentVersionName,
                    onOpenAbout = { showAboutDialog = true }
                )
            }
        }
    }

    // About dialog modal
    AboutDialog(
        isOpen = showAboutDialog,
        versionName = viewModel.currentVersionName,
        developerName = "PasturTheFox",
        githubUrl = "https://github.com/pastur253/BTController",
        updateState = updateState,
        onCheckUpdates = { viewModel.checkForUpdates(isManual = true) },
        onDismiss = { showAboutDialog = false }
    )

    // Auto-update dialog modal
    UpdateDialog(
        state = updateState,
        currentVersion = viewModel.currentVersionName,
        canInstallPackages = viewModel.canRequestPackageInstalls(),
        onDownload = { releaseInfo -> viewModel.startUpdateDownload(releaseInfo) },
        onCancelDownload = { viewModel.cancelUpdateDownload() },
        onInstall = { file ->
            val result = viewModel.installUpdate(file)
            if (result is UpdateInstaller.InstallResult.PermissionRequired) {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    installPermissionLauncher.launch(
                        Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${context.packageName}"))
                    )
                }
            }
        },
        onRequestInstallPermission = {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                installPermissionLauncher.launch(
                    Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${context.packageName}"))
                )
            }
        },
        onDismiss = { ignoreVersion -> viewModel.dismissUpdate(ignoreVersion) },
        onRetry = { viewModel.checkForUpdates(isManual = true) }
    )
}

@Composable
fun AppVersionCard(
    versionName: String,
    onOpenAbout: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp, bottom = 16.dp)
            .clip(RoundedCornerShape(16.dp))
            .clickable { onOpenAbout() },
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.35f)
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column {
                Text(
                    text = "BT Controller v$versionName",
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 13.sp,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = "GitHub: pastur253/BTController",
                    fontSize = 11.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            OutlinedButton(
                onClick = onOpenAbout,
                shape = RoundedCornerShape(10.dp),
                contentPadding = PaddingValues(horizontal = 12.dp, vertical = 6.dp)
            ) {
                Icon(
                    painter = painterResource(R.drawable.ic_info),
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                    tint = PrimaryBlue
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text(
                    text = "О приложении",
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Medium,
                    color = PrimaryBlue
                )
            }
        }
    }
}

@Composable
fun SectionHeader(title: String, subtitle: String) {
    Column(modifier = Modifier.fillMaxWidth()) {
        Text(
            text = title,
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.onBackground
        )
        Text(
            text = subtitle,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

@Composable
fun DeviceCard(
    device: BluetoothDeviceModel,
    isWidgetDevice: Boolean,
    modifier: Modifier = Modifier,
    orderIndex: Int? = null,
    isDragging: Boolean = false,
    canMoveUp: Boolean = false,
    canMoveDown: Boolean = false,
    onMoveUp: (() -> Unit)? = null,
    onMoveDown: (() -> Unit)? = null,
    onDragStart: ((String) -> Unit)? = null,
    onDrag: ((Float) -> Unit)? = null,
    onDragEnd: (() -> Unit)? = null,
    onDragCancel: (() -> Unit)? = null,
    onToggleConnection: () -> Unit,
    onToggleWidget: () -> Unit
) {
    Card(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(
            containerColor = if (isDragging) MaterialTheme.colorScheme.surfaceVariant else MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = if (isDragging) 8.dp else 2.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 10.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            // Reorder & Drag controls for widget devices
            if (isWidgetDevice) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center,
                    modifier = Modifier.padding(end = 4.dp)
                ) {
                    IconButton(
                        onClick = onMoveUp ?: {},
                        enabled = canMoveUp,
                        modifier = Modifier.size(20.dp)
                    ) {
                        Icon(
                            painter = painterResource(R.drawable.ic_arrow_up),
                            contentDescription = "Вверх",
                            tint = if (canMoveUp) PrimaryBlue else MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.2f),
                            modifier = Modifier.size(14.dp)
                        )
                    }

                    Box(
                        modifier = Modifier
                            .size(width = 30.dp, height = 24.dp)
                            .then(
                                if (onDrag != null && onDragStart != null && onDragEnd != null && onDragCancel != null) {
                                    Modifier.pointerInput(device.address) {
                                        detectDragGestures(
                                            onDragStart = { onDragStart(device.address) },
                                            onDrag = { change, dragAmount ->
                                                change.consume()
                                                onDrag(dragAmount.y)
                                            },
                                            onDragEnd = onDragEnd,
                                            onDragCancel = onDragCancel
                                        )
                                    }
                                } else Modifier
                            ),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            painter = painterResource(R.drawable.ic_drag_handle),
                            contentDescription = "Перетащить для смены порядка",
                            tint = if (isDragging) PrimaryBlue else MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f),
                            modifier = Modifier.size(20.dp)
                        )
                    }

                    IconButton(
                        onClick = onMoveDown ?: {},
                        enabled = canMoveDown,
                        modifier = Modifier.size(20.dp)
                    ) {
                        Icon(
                            painter = painterResource(R.drawable.ic_arrow_down),
                            contentDescription = "Вниз",
                            tint = if (canMoveDown) PrimaryBlue else MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.2f),
                            modifier = Modifier.size(14.dp)
                        )
                    }
                }
            }

            // Device Type Icon
            Box(
                modifier = Modifier
                    .size(42.dp)
                    .clip(CircleShape)
                    .background(
                        if (device.isConnected) StatusConnected.copy(alpha = 0.15f)
                        else PrimaryBlue.copy(alpha = 0.12f)
                    ),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    painter = painterResource(
                        if (device.deviceType == DeviceType.HEADPHONES) R.drawable.ic_headphones
                        else R.drawable.ic_bluetooth
                    ),
                    contentDescription = null,
                    tint = if (device.isConnected) StatusConnected else PrimaryBlue,
                    modifier = Modifier.size(22.dp)
                )
            }

            Spacer(modifier = Modifier.width(10.dp))

            // Name & Status
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = if (orderIndex != null) "#$orderIndex  ${device.name}" else device.name,
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 15.sp,
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1
                )
                Spacer(modifier = Modifier.height(2.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .size(8.dp)
                            .clip(CircleShape)
                            .background(
                                when {
                                    device.isDisconnecting -> StatusDisconnecting
                                    device.isConnecting -> StatusConnecting
                                    device.isConnected -> StatusConnected
                                    else -> StatusDisconnected
                                }
                            )
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    Text(
                        text = when {
                            device.isDisconnecting -> "Отключение..."
                            device.isConnecting -> "Подключение..."
                            device.isConnected -> "Подключено"
                            else -> "Отключено"
                        },
                        fontSize = 12.sp,
                        color = when {
                            device.isDisconnecting -> StatusDisconnecting
                            device.isConnecting -> StatusConnecting
                            device.isConnected -> StatusConnected
                            else -> MaterialTheme.colorScheme.onSurfaceVariant
                        }
                    )
                }
            }

            Spacer(modifier = Modifier.width(8.dp))

            // Quick Connect / Disconnect Button
            val isBusy = device.isConnecting || device.isDisconnecting
            Button(
                onClick = onToggleConnection,
                enabled = !isBusy,
                shape = RoundedCornerShape(10.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = when {
                        device.isDisconnecting -> StatusDisconnecting
                        device.isConnecting -> StatusConnecting
                        device.isConnected -> ErrorRed.copy(alpha = 0.85f)
                        else -> PrimaryBlue
                    },
                    disabledContainerColor = if (device.isDisconnecting) StatusDisconnecting else StatusConnecting
                ),
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 4.dp),
                modifier = Modifier.height(34.dp)
            ) {
                Text(
                    text = when {
                        isBusy -> "..."
                        device.isConnected -> "Откл"
                        else -> "Вкл"
                    },
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Bold,
                    color = Color.White
                )
            }

            Spacer(modifier = Modifier.width(4.dp))

            // Add / Remove from widget button
            IconButton(
                onClick = onToggleWidget,
                modifier = Modifier.size(34.dp)
            ) {
                Icon(
                    painter = painterResource(if (isWidgetDevice) R.drawable.ic_delete else R.drawable.ic_add),
                    contentDescription = if (isWidgetDevice) "Удалить из виджета" else "Добавить в виджет",
                    tint = if (isWidgetDevice) ErrorRed else PrimaryBlue,
                    modifier = Modifier.size(22.dp)
                )
            }
        }
    }
}

@Composable
fun DiscoveredDeviceCard(
    device: BluetoothDeviceModel,
    onPair: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                painter = painterResource(R.drawable.ic_bluetooth),
                contentDescription = null,
                tint = PrimaryBlue,
                modifier = Modifier.size(24.dp)
            )
            Spacer(modifier = Modifier.width(12.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = device.name,
                    fontWeight = FontWeight.Medium,
                    fontSize = 14.sp,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = device.address,
                    fontSize = 11.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            OutlinedButton(
                onClick = onPair,
                shape = RoundedCornerShape(8.dp),
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 4.dp),
                modifier = Modifier.height(32.dp)
            ) {
                Text(text = "Сопряжение", fontSize = 11.sp)
            }
        }
    }
}

@Composable
fun EmptyWidgetDevicesCard() {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f))
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(20.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Icon(
                painter = painterResource(R.drawable.ic_settings),
                contentDescription = null,
                tint = PrimaryBlue,
                modifier = Modifier.size(36.dp)
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "Виджет пуст",
                fontWeight = FontWeight.Bold,
                fontSize = 15.sp,
                color = MaterialTheme.colorScheme.onSurface
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = "Добавьте сопряженные устройства ниже, чтобы они появились на домашнем экране.",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                lineHeight = 16.sp
            )
        }
    }
}

@Composable
fun PermissionCard(onRequest: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                text = "Требуются разрешения Bluetooth",
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onErrorContainer
            )
            Text(
                text = "Для сканирования, подключения к устройствам и работы виджета приложению необходимы разрешения.",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onErrorContainer.copy(alpha = 0.8f),
                modifier = Modifier.padding(vertical = 6.dp)
            )
            Button(
                onClick = onRequest,
                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
            ) {
                Text(text = "Предоставить разрешения")
            }
        }
    }
}

@Composable
fun BluetoothDisabledCard(onEnable: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = StatusConnecting.copy(alpha = 0.15f))
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                painter = painterResource(R.drawable.ic_bluetooth),
                contentDescription = null,
                tint = StatusConnecting,
                modifier = Modifier.size(32.dp)
            )
            Spacer(modifier = Modifier.width(12.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = "Bluetooth выключен",
                    fontWeight = FontWeight.Bold,
                    fontSize = 14.sp
                )
                Text(
                    text = "Включите Bluetooth для управления устройствами",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            Button(
                onClick = onEnable,
                colors = ButtonDefaults.buttonColors(containerColor = StatusConnecting)
            ) {
                Text(text = "Включить", color = Color.Black, fontWeight = FontWeight.Bold)
            }
        }
    }
}
