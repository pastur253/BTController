# BT Controller (Android & Windows 11)

[![Android CI](https://github.com/pastur253/BTController/actions/workflows/android.yml/badge.svg)](https://github.com/pastur253/BTController/actions/workflows/android.yml)
[![Windows CI](https://github.com/pastur253/BTController/actions/workflows/windows.yml/badge.svg)](https://github.com/pastur253/BTController/actions/workflows/windows.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Android](https://img.shields.io/badge/Android-8.0%2B%20(API%2026--36)-brightgreen.svg)](https://developer.android.com)
[![Windows 11](https://img.shields.io/badge/Windows-11%20(WinUI%203)-0078D4.svg)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Kotlin](https://img.shields.io/badge/Kotlin-2.0.21-purple.svg)](https://kotlinlang.org)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com)
[![Compose](https://img.shields.io/badge/Jetpack%20Compose-Material%203-4285F4.svg)](https://developer.android.com/jetpack/compose)

Кроссплатформенное решение для быстрого управления и переключения Bluetooth-устройств (наушники, гарнитуры, колонки, периферия) через интерактивные виджеты на **Android** (AppWidget на рабочем столе) и **Windows 11** (панель мини-приложений Widgets Board по `Win + W`).

---

## 📥 Скачать приложение / Download

<p align="center">
  <a href="https://github.com/pastur253/BTController/releases/latest/download/BTController-release.apk">
    <img src="https://img.shields.io/badge/Скачать%20для-Android%20(APK)-3DDC84?style=for-the-badge&logo=android&logoColor=white" alt="Скачать для Android" />
  </a>
  &nbsp;&nbsp;&nbsp;&nbsp;
  <a href="https://github.com/pastur253/BTController/releases/latest/download/BTController-Setup.exe">
    <img src="https://img.shields.io/badge/Скачать%20для-Windows%20(Setup.exe)-0078D4?style=for-the-badge&logo=windows11&logoColor=white" alt="Скачать для Windows" />
  </a>
</p>

### Прямые ссылки на загрузку последних версий:

| Платформа | Кнопка скачивания | Файл | Описание |
| :--- | :--- | :--- | :--- |
| **Android** | [![Скачать APK](https://img.shields.io/badge/Скачать-APK-3DDC84?style=flat-square&logo=android&logoColor=white)](https://github.com/pastur253/BTController/releases/latest/download/BTController-release.apk) | `BTController-release.apk` | Клиент Jetpack Compose и домашний виджет (Android 8.0+) |
| **Windows 11** | [![Скачать Setup.exe](https://img.shields.io/badge/Скачать-Setup.exe-0078D4?style=flat-square&logo=windows11&logoColor=white)](https://github.com/pastur253/BTController/releases/latest/download/BTController-Setup.exe) | `BTController-Setup.exe` | Единый установщик (сертификат + MSIX + виджет `Win + W`) |

> [!TIP]
> Все версии, исходные коды и подробный список изменений доступны в разделе **[GitHub Releases](https://github.com/pastur253/BTController/releases)**.

---

## 📱 Основные возможности (Android)

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
- **Автоматическая проверка**: при запуске приложение проверяет наличие свежих релизов в GitHub-репозитории `pastur253/BTController`. Проверку при открытии можно в любой момент включить или выключить переключателем в карточке версии на главном экране или в диалоге «О приложении».
- **Ручная проверка**: кнопка обновления в диалоге «О приложении» и карточка версии внизу экрана с индикатором новых версий.
- **Интерактивный диалог**:
  - Показывает разницу версий (текущая ➔ новая).
  - Отображает список изменений (Changelog / Release notes) и размер APK-файла.
  - Опции: «Скачать и установить», «Пропустить эту версию», «Напомнить позже», «Открыть на GitHub».
- **Загрузка и установка**:
  - Фоновая загрузка APK с отображением прогресса (в процентах и мегабайтах).
  - Корректная обработка редиректов GitHub Releases / CDN.
  - Безопасная установка через `FileProvider` (`content://`) и запрос разрешения `REQUEST_INSTALL_PACKAGES` на Android 8.0+.

---

## 🪟 Основные возможности (Windows 11)

Подробная документация по клиенту Windows доступна в [windows/README.md](windows/README.md).

### 1. Виджет в панели мини-приложений (Windows Widgets Board, `Win + W`)
- **Мгновенное переключение:** Кнопка «Вкл / Откл» для каждого выбранного устройства прямо внутри карточки виджета без открытия приложения.
- **Индикация статуса:** Цветовые статусы (🟢 Подключено, 🟡 Подключение..., ⚪ Отключено) и уровень заряда батареи.
- **Размеры карточек:** Поддержка стандартного `Medium` и расширенного `Large` на базе Adaptive Cards.
- **Интерактивные действия:** Кнопка быстрого обновления `↻` и переход в настройки.

### 2. Приложение WinUI 3 (Fluent Design)
- **Mica Backdrop:** Адаптивный полупрозрачный фон окна, реагирующий на обои рабочего стола.
- **Темы оформления:** Автоматическое следование системной теме Windows (Светлая / Темная) либо ручной выбор.
- **Управление виджетом:** Добавление/удаление устройств и изменение порядка отображения (кнопки Вверх / Вниз).
- **Сопряженные устройства:** Полный список сопряженных Bluetooth-устройств ПК с кнопкой быстрого добавления в виджет.
- **Сканер окружения:** Поиск новых Bluetooth-устройств в режиме обнаружения с сопряжением в один клик.

---

## 🏗 Архитектура проекта

```
BTController/
├── app/                                  # Клиент для Android (Kotlin & Jetpack Compose)
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
│   │   └── res/                                  # XML разметка виджета, ресурсы и иконки
│
└── windows/                              # Клиент для Windows 11 (C# & WinUI 3)
    ├── BTController.Core/                 # Общая бизнес-логика и Bluetooth API (WinRT)
    ├── BTController.Widgets/              # COM-провайдер виджета для Windows 11 Widgets Board
    ├── BTController.App/                  # Нативное WinUI 3 Fluent приложение
    └── BTController.Installer/            # Автономный инсталлятор с авторегистрацией виджета
```

---

## ⚙️ Технические особенности реализации Bluetooth

### Android:
- Для управления аудиоустройствами (A2DP, Гарнитуры) и периферией (HID) используется `BluetoothProfile.ServiceListener` для получения прокси `BluetoothA2dp`, `BluetoothHeadset` и `HID Host`.
- Подключение/отключение выполняется через методы профилей с автоматическим fallback на BLE GATT и системные интенты.
- Системные события `ACTION_ACL_CONNECTED`, `ACTION_ACL_DISCONNECTED`, `ACTION_STATE_CHANGED` мгновенно отслеживаются `BluetoothConnectionReceiver` и синхронизируют виджет без задержек.

### Windows 11:
- Взаимодействие через нативное WinRT API: `Windows.Devices.Bluetooth.BluetoothDevice` и `Windows.Devices.Enumeration.DeviceInformation`.
- Поддержка фонового COM-сервера `IWidgetProvider` для работы карточки в панели `Win + W` даже при закрытом основном окне приложения.

---

## 🚀 Сборка и запуск

### Android
#### Через Android Studio:
1. Откройте проект в **Android Studio** (`File -> Open -> BTController`).
2. Дождитесь автоматической синхронизации Gradle.
3. Выберите подключенное устройство или эмулятор и нажмите **Run** (`Shift+F10`).
4. На домашнем экране Android зажмите свободное место, выберите **Виджеты -> BT Controller** и перетащите виджет на экран.

#### Через командную строку (CLI):
```bash
# Debug APK
./gradlew assembleDebug

# Release APK
./gradlew assembleRelease
```
Файлы будут сформированы в `app/build/outputs/apk/`.

### Windows 11
#### Через Visual Studio 2022:
1. Откройте `windows/BTController.sln` в **Visual Studio 2022**.
2. Выберите конфигурацию **`Release`** и платформу **`x64`**.
3. Запустите проект **`BTController.App`** (`F5`).

#### Через командную строку (.NET CLI):
```powershell
# Сборка приложения
dotnet publish windows/BTController.App/BTController.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64

# Сборка инсталлятора
dotnet publish windows/BTController.Installer/BTController.Installer.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64
```

---

## 🤝 Вклад в проект (Contributing)

Подробнее о том, как сообщить об ошибке или предложить новую функцию, читайте в [CONTRIBUTING.md](CONTRIBUTING.md).

---

## 📄 Лицензия

Проект распространяется под открытой лицензией [MIT License](LICENSE).
