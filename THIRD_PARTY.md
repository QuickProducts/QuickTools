# Сторонние компоненты

| Компонент | Назначение | Источник |
|---|---|---|
| .NET 8 / WPF | Runtime и интерфейс, runtime включён в дистрибутив | https://github.com/dotnet/runtime · https://github.com/dotnet/wpf |
| FFmpeg / ffprobe | Аудио/видео и проверка | https://ffmpeg.org · https://github.com/GyanD/codexffmpeg/releases |
| ImageMagick | Изображения | https://github.com/ImageMagick/ImageMagick/releases |
| Pandoc | Документы | https://github.com/jgm/pandoc/releases |
| Typst | Создание PDF | https://github.com/typst/typst/releases |
| Poppler for Windows | Чтение PDF | https://github.com/oschwartz10612/poppler-windows/releases |

У инструментов собственные лицензии и уведомления, которые остаются в распакованных пакетах. Лицензия сборки FFmpeg зависит от включённых возможностей; проверяйте документы выбранного пакета при самостоятельном распространении.

URL, версии и SHA-256 загрузчика находятся в [DependencyManager.cs](DependencyManager.cs). Обновляйте URL и контрольную сумму вместе после проверки источника.

Графические материалы QuickTools находятся в `assets/`, промпты генерации сохранены рядом. Внешние шрифты не включены; используются системные шрифты Windows.
