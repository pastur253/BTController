# BT Controller для Windows 11 (WinUI 3 & Widgets Board)

Нативная реализация **Bluetooth Controller** для **Windows 11** с использованием **WinUI 3 (Windows App SDK)** и интеграцией с системной панелью мини-приложений (**Windows Widgets Board**, вызываемой клавишами **Win + W**).

---

## 📌 Архитектура решения

Решение расположено в папке `windows/` и состоит из четырех взаимосвязанных проектов:

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
├── BTController.App/                     # Основное приложение WinUI 3
│   ├── MainWindow.xaml                   # Окно с нативным эффектом Mica и TitleBar
│   ├── ViewModels/                       # Dashboard, Paired, Discovery, Settings
│   └── Views/                            # Страницы со списком виджета, сопряженными устройствами и сканером
└── BTController.Package/                 # MSIX Packaging проект
    └── Package.appxmanifest              # Декларация com.microsoft.windows.widgets.provider и COM-сервера
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

## 🛠 Сборка и запуск

### Вариант 1: Через Visual Studio
1. Откройте `windows/BTController.sln` в **Visual Studio 2022** (с установленной рабочей нагрузкой *.NET Desktop Development* и *Windows App SDK C# Templates*).
2. Выберите конфигурацию `Debug` и платформу `x64`.
3. Установите `BTController.App` (или `BTController.Package`) как запускаемый проект и нажмите **F5**.

### Вариант 2: Через командную строку (CLI)
Сборка основного WinUI 3 приложения:
```powershell
& "MSBuild.exe" windows\BTController.App\BTController.App.csproj /p:Configuration=Debug /p:Platform=x64 /p:UseSharedCompilation=false
```
Собранный исполняемый файл будет доступен по пути:
`windows\BTController.App\bin\x64\Debug\net8.0-windows10.0.26100.0\BTController.App.exe`

---

## 📲 Как включить виджет в Windows 11

1. Запустите или установите пакет приложения `BTController.Package`.
2. Откройте панель мини-приложений нажатием **Win + W** (или кликом по виджету погоды на панели задач).
3. Нажмите кнопку **`+`** («Добавить виджеты») в правом верхнем углу панели.
4. В списке доступных виджетов найдите **Bluetooth Controller** и нажмите **Закрепить**.
