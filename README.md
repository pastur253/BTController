# BT Controller (Android App & Widget)

[![Android CI](https://github.com/pastur253/BTController/actions/workflows/android.yml/badge.svg)](https://github.com/pastur253/BTController/actions/workflows/android.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Android](https://img.shields.io/badge/Android-8.0%2B%20(API%2026--36)-brightgreen.svg)](https://developer.android.com)
[![Kotlin](https://img.shields.io/badge/Kotlin-2.0.21-purple.svg)](https://kotlinlang.org)
[![Compose](https://img.shields.io/badge/Jetpack%20Compose-Material%203-4285F4.svg)](https://developer.android.com/jetpack/compose)

Полнофункциональное Android-приложение с интерактивным домашним виджетом для быстрого управления и переключения Bluetooth-устройств (наушники, колонки, гарнитуры и др.).

---

## 📥 Скачать приложение

Готовые APK-файлы доступны в разделе **[Releases](https://github.com/pastur253/BTController/releases)**:
- **`BTController-release.apk`** — оптимизированная релизная версия приложения.
- **`BTController-debug.apk`** — отладочная сборка.

---

## 📱 Основные возможности

### 1. Интерактивный виджет для рабочего стола (AppWidget)
- Отображает список выбранных пользователем Bluetooth-устройств.
- Показывает статус подключения в реальном времени:
  - 🟢 **Подключено**
  - 🟡 **Подключение...**
  - ⚪ **Отключено**
- Кнопка быстрого переключения в один клик: **«Вкл» / «Откл»**.
- Кнопка быстрого перехода в настройки приложения (`⚙️`) и принудительного обновления (`🔄`).
- Если список пуст — виджет содержит подсказку и при нажатии открывает приложение для добавления устройств.

### 2. Интерфейс приложения (Jetpack Compose Material 3)
- **Устройства в виджете**: список добавленных на рабочий стол устройств с возможностью в один клик удалить устройство из виджета или переключить статус соединения.
- **Сопряженные устройства**: автоматическое считывание всех привязанных к смартфону устройств с кнопкой `+` для быстрого добавления в виджет.
- **Поиск новых устройств (Bluetooth Discovery)**: встроенный сканер ближайших Bluetooth-устройств с возможностью сопряжения в один тап.
- **Управление разрешениями**: адаптивная поддержка Android 12+ (API 31–36, `BLUETOOTH_CONNECT`, `BLUETOOTH_SCAN`) и Android 8–11 (`BLUETOOTH`, `BLUETOOTH_ADMIN`, `ACCESS_FINE_LOCATION`).

### 3. Автообновление из удаленного репозитория (GitHub Releases)
- **Автоматическая проверка**: при запуске приложение проверяет наличие свежих релизов в GitHub-репозитории `pastur253/BTController`.
- **Ручная проверка**: кнопка обновления в шапке (TopAppBar) и карточка версии внизу экрана с индикатором новых версий.
- **Интерактивный диалог**:
  - Показывает разницу версий (текущая ➔ новая).
  - Отображает список изменений (Changelog / Release notes) и размер APK-файла.
  - Опции: «Скачать и установить», «Пропустить эту версию», «Напомнить позже», «Открыть на GitHub».
- **Загрузка и установка**:
  - Фоновая загрузка APK с отображением прогресса (в процентах и мегабайтах).
  - Корректная обработка редиректов GitHub Releases / CDN.
  - Безопасная установка через `FileProvider` (`content://`) и запрос разрешения `REQUEST_INSTALL_PACKAGES` на Android 8.0+.

---

## 🏗 Архитектура проекта

```
BTController/
├── app/
│   ├── src/main/
│   │   ├── AndroidManifest.xml
│   │   ├── java/com/bluetoothcontroller/app/
│   │   │   ├── BTApplication.kt                  # Точка входа приложения
│   │   │   ├── bluetooth/
│   │   │   │   ├── BluetoothController.kt        # Управление профилями (A2DP, Headset, HID), соединением и сканированием
│   │   │   │   └── BluetoothConnectionReceiver.kt# Мониторинг системных событий BT (ACL_CONNECTED/DISCONNECTED)
│   │   │   ├── data/
│   │   │   │   ├── BluetoothDeviceModel.kt       # Модель устройства (MAC, имя, тип, статус)
│   │   │   │   └── WidgetPreferences.kt          # Хранение списка устройств виджета (SharedPreferences)
│   │   │   ├── widget/
│   │   │   │   ├── BluetoothWidgetProvider.kt    # AppWidgetProvider (обновление, PendingIntent)
│   │   │   │   ├── BluetoothWidgetService.kt     # RemoteViewsService & Factory для списка ListView
│   │   │   │   └── WidgetActionReceiver.kt       # Обработка кликов «Вкл/Откл» из виджета
│   │   │   ├── update/
│   │   │   │   ├── ReleaseInfo.kt                # Модели данных релиза и состояния обновления (UpdateState)
│   │   │   │   ├── UpdateChecker.kt              # Запрос к GitHub Releases API и семантическое сравнение версий
│   │   │   │   ├── UpdateDownloader.kt           # Асинхронное скачивание APK с подсчетом прогресса
│   │   │   │   ├── UpdateInstaller.kt            # Установка APK через FileProvider и проверка разрешений
│   │   │   │   ├── UpdateManager.kt              # Координатор автообновления и StateFlow
│   │   │   │   └── UpdatePreferences.kt          # Настройки (пропуск версий, интервалы)
│   │   │   └── ui/
│   │   │       ├── MainActivity.kt               # Jetpack Compose UI
│   │   │       ├── MainViewModel.kt              # StateFlow и взаимодействие с контроллером
│   │   │       ├── UpdateDialog.kt               # UI модального окна загрузки и установки обновления
│   │   │       └── theme/                        # Тема, цвета и типографика Material 3
│   │   └── res/
│   │       ├── drawable/                         # Векторные иконки, стили кнопок и фон виджета
│   │       ├── layout/                           # XML-разметка виджета (widget_layout, widget_device_item)
│   │       ├── values/                           # Строки, цвета
│   │       └── xml/                              # Метаданные виджета и file_paths для FileProvider
```

---

## ⚙️ Технические детали подключения Bluetooth

Для управления аудиоустройствами (A2DP, Гарнитуры) и периферией (HID):
1. Используется `BluetoothProfile.ServiceListener` для получения прокси `BluetoothA2dp`, `BluetoothHeadset` и `HID Host`.
2. Подключение/отключение выполняется через методы профилей с автоматическим fallback на BLE GATT и системные интенты.
3. Системные события `ACTION_ACL_CONNECTED`, `ACTION_ACL_DISCONNECTED`, `ACTION_STATE_CHANGED` мгновенно отслеживаются `BluetoothConnectionReceiver` и вызывают `notifyAppWidgetViewDataChanged` для мгновенной синхронизации виджета без задержек.

---

## 🚀 Запуск и сборка

### Сборка через Android Studio
1. Откройте проект в **Android Studio** (`File -> Open -> BTController`).
2. Дождитесь автоматической синхронизации Gradle.
3. Выберите подключенное устройство или эмулятор и нажмите **Run** (`Shift+F10`).
4. На домашнем экране Android зажмите свободное место, выберите **Виджеты -> BT Controller** и перетащите виджет на экран.

### Сборка через командную строку (CLI)
Сборка debug APK:
```bash
./gradlew assembleDebug
```
Сборка release APK:
```bash
./gradlew assembleRelease
```
Файлы будут сформированы в `app/build/outputs/apk/`.

---

## 🤝 Вклад в проект (Contributing)

Подробнее о том, как сообщить об ошибке или предложить новую функцию, читайте в [CONTRIBUTING.md](CONTRIBUTING.md).

---

## 📄 Лицензия

Проект распространяется под открытой лицензией [MIT License](LICENSE).
