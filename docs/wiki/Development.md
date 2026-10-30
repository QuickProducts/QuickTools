# Сборка и разработка

Нужны Windows x64, Git и .NET SDK 8.

```powershell
git clone https://github.com/QuickProducts/QuickTools.git
cd QuickTools
./build.ps1
./build.ps1 -Publish
./build-setup.ps1
```

Приложение публикуется в `dist`, установщик — `release/QuickToolsSetup.exe`. Локальный SDK в `.sdk` поддерживается скриптами, но не хранится в репозитории. Бинарные движки загружаются отдельно. Сгенерированные сборки, payload установщика, журналы и тестовые файлы исключены через `.gitignore`.

Подробности структуры и диагностических запусков: [DEVELOPMENT.md](https://github.com/QuickProducts/QuickTools/blob/main/docs/DEVELOPMENT.md). Итоги локальных проверок: [validation](https://github.com/QuickProducts/QuickTools/tree/main/docs/validation).

При добавлении формата изменяйте маршрутизацию и проверку результата совместно. Не подменяйте конвертацию изменением расширения. Не переносите Shell/UI Automation, длительные вычисления или ожидание внешнего процесса в обработчик ввода UI.

CI собирает приложение и установщик на Windows. Локальные регрессионные сценарии проверяют синтетические файлы и не гарантируют работу каждого возможного кодека, документа или расширения Explorer.
