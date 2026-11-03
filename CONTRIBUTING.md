# Руководство по внесению вклада (Contributing)

Мы рады вашему интересу к развитию проекта **BT Controller**!

## Как помочь проекту?

1. **Сообщить об ошибке**: если вы нашли баг, [создайте issue](../../issues/new?template=bug_report.md) с описанием шагов воспроизведения и версии Android.
2. **Предложить улучшение**: опишите вашу идею в [feature request](../../issues/new?template=feature_request.md).
3. **Отправить Pull Request**: исправления ошибок и новые возможности приветствуются.

## Разработка

1. Сделайте Fork репозитория.
2. Клонируйте форк себе на компьютер:
   ```bash
   git clone https://github.com/<ваш-аккаунт>/BTController.git
   cd BTController
   ```
3. Откройте проект в **Android Studio Ladybug** (или новее). Рекомендуемая версия JDK — 17+.
4. Создайте новую ветку для ваших изменений:
   ```bash
   git checkout -b feature/awesome-feature
   ```
5. Проверьте сборку перед коммитом:
   ```bash
   ./gradlew assembleDebug
   ```
6. Сделайте коммит и отправьте ветку в свой форк:
   ```bash
   git commit -m "feat: add support for new bluetooth profile"
   git push origin feature/awesome-feature
   ```
7. Откройте Pull Request в ветку `main` основного репозитория.

## Лицензия

Отправляя код в репозиторий, вы соглашаетесь с тем, что ваш вклад будет лицензирован на условиях [MIT License](LICENSE).
