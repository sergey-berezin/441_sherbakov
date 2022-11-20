Это корневой каталог для лабораторных работ по предмету: *ООП, технологии NET*

На данных момент тут находятся 4 папки с лабораторными работами:

1. **Lab1** - библиотека, в которой создается NuGet-пакет и консольное приложение, использующее его *(подробнее см. в файле README внутри каталога)*
2. **Lab2** - WPF-приложение, использующее NuGet-пакет, создаваемый библиотекой из *Lab1*, предоставляющий пользовательский интерфейс для использования модели машинного обучения для определения вероятности эмоций на изображении. *(подробнее см. в файле README внутри соответствующего каталога)*
3. **Lab3** - WPF-приложение, также использующее NuGet-пакет, создаваемый библиотекой *Lab1*, и также для хранения результатов анализа изображений использующее базу данных (Emotions.db) *(подробнее см. в файле README внутри соответствующего каталога)*
4. **Lab4** - Сервер и WPF-клиент. Сервер проводит вычисления и использует базу данных (Emotions.db) для хранения результатов. Клиент с помощью http-запросов получает информацию от Сервера. Также добавлена поддержка OpenApi для сервера. Создано простейшее консольное C# приложение на основе спецификации OpenAPI .json файла. (Сам WPF-клиент невозможно было сгенерировать тоже из .json-файла из-за ошибки). *(подробнее см. в файле README внутри соответствующего каталога)