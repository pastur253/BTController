# BT Controller для Windows 11 (WinUI 3 & Widgets Board)

Нативная реализация **Bluetooth Controller** для **Windows 11** с использованием **WinUI 3 (Windows App SDK Single-Project MSIX)** и интеграцией с системной панелью мини-приложений (**Windows Widgets Board**, вызываемой клавишами **Win + W**).

---

## 📌 Архитектура решения

Решение расположено в папке `windows/` и состоит из трех основных проектов (Single-Project архитектура WinUI 3):

```
windows/
├── BTController.sln                      # Главный файл решения Visual Studio
├── BTController.Core/                    # Общая бизнес-логика и Bluetooth API
│   ├── Models/
│   │   ├── BluetoothDeviceModel.cs       # Реактивная модель устройства (MVVM ObservableObject)
│   │   └── DeviceType.cs                 # Типы устройств (Headphones, Speaker, Phone, etc.)
│   └── Services/
│       ├── IBluetoothService.cs          # Контракт управления Bluetooth
│       ├── WindowsBluetoothService.cs    # Реализация на базе Windows.Devices.Bluetooth (WinRT)
│       └── WidgetPreferencesService.cs   # Хранилище закрепленных в виджете устройств (JSON)
├── BTController.Widgets/                 # Провайдер виджета для Windows 11 Widgets Board
│   ├── BluetoothWidgetProvider.cs        # Реализация IWidgetProvider & IWidgetProvider2 (COM-сервер)
│   ├── WidgetPayloadHelper.cs            # Сериализатор данных для Adaptive Cards
│   └── Templates/
│       ├── WidgetTemplateMedium.json     # Шаблон адаптивной карточки (Medium)
│       └── WidgetTemplateLarge.json      # Шаблон адаптивной карточки (Large с деталями)
└── BTController.App/                     # Основное приложение WinUI 3 (Single-Project Packaging)
    ├── Package.appxmanifest              # Манифест MSIX с регистрацией виджета (Win + W)
    ├── Assets/                           # Иконки, плитки и скриншоты для виджета
    ├── MainWindow.xaml                   # Окно с нативным эффектом Mica и TitleBar
    ├── ViewModels/                       # Dashboard, Paired, Discovery, Settings
    └── Views/                            # Страницы со списком виджета, сопряженными устройствами и сканером
```

---

## 🚀 Основные возможности

### 1. Виджет в панели мини-приложений Windows 11 (Win + W)
- **Мгновенное переключение:** Кнопка «Вкл / Откл» для каждого выбранного устройства прямо внутри карточки виджета без открытия приложения.
- **Индикация статуса:** Цветовые статусы (🟢 Подключено, 🟡 Подключение..., ⚪ Отключено).
- **Размеры карточек:** Поддержка стандартного `Medium` и расширенного `Large` с отображением процента батареи.
- **Интерактивные действия:** Кнопка быстрого принудительного обновления `↻` и переход в настройки.

### 2. Приложение WinUI 3 (Fluent Design)
- **Mica Backdrop:** Адаптивный полупрозрачный фон окна, реагирующий на обои рабочего стола.
- **Темы оформления:** Автоматическое следование системной теме Windows (Светлая / Темная) либо ручной выбор.
- **Управление виджетом:** Добавление/удаление устройств и изменение порядка отображения (кнопки Вверх / Вниз).
- **Сопряженные устройства:** Полный список сопряженных Bluetooth-устройств ПК с кнопкой быстрого добавления в виджет.
- **Сканер Bluetooth-окружения:** Поиск новых устройств в режиме обнаружения с анимированным `ProgressRing` и сопряжением в один клик.

---

## 🛠 Сборка и запуск в Visual Studio 2022 / CLI

1. Откройте `windows/BTController.sln` в **Visual Studio 2022** (или используйте `msbuild` / `dotnet build`).
2. В панели инструментов выберите платформу **`x64`** и конфигурацию **`Debug`** (или `Release`).
3. Убедитесь, что запускаемым проектом выбран **`BTController.App`** (ПКМ по `BTController.App` -> *Назначить запускаемым проектом*).
4. Нажмите **F5** (или зеленую стрелку «Запустить»). Приложение соберётся в **Single-Project MSIX** и зарегистрирует провайдер виджетов.
5. Для сборки `.msix` из командной строки:
   ```powershell
   dotnet build windows/BTController.App/BTController.App.csproj -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true
   ```
   > **Примечание:** Для подписи `.msix` в CI используются секреты `WINDOWS_PFX_BASE64` и `WINDOWS_PFX_PASSWORD` (Subject сертификата должен совпадать с `Publisher` в `Package.appxmanifest`). Старые unpackaged-установщики (`BTController.Installer` и `installer.iss`) несовместимы с Win+W без Package Identity и оставлены как legacy.

---

## 📲 Как включить виджет в Windows 11

1. Запустите приложение хотя бы один раз (при первом запуске Windows регистрирует провайдер виджета).
2. Откройте панель мини-приложений нажатием **Win + W** (или кликом по виджету погоды на панели задач).
3. Нажмите кнопку **`+`** («Добавить виджеты») в правом верхнем углу панели.
4. В списке доступных виджетов найдите **Bluetooth Controller** и нажмите **Закрепить**.
