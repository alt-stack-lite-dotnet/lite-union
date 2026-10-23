# Changelog

Формат: [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/), версии по [SemVer](https://semver.org/lang/ru/).

## [1.0.0-rc.1]

Первый публичный релиз-кандидат.

### Добавлено

- Генератор union'ов: `[Union<T1, ..., Tn>]` на `partial struct`, от 2 до 16 кейсов. Для каждого кейса `FromT`, неявное приведение, `IsT`, `TryGetT`, `GetT`; а также `Kind`, `Match`, `Switch`, `IEquatable<T>`, `Equals`, `GetHashCode`, `ToString`, `==`, `!=`.
- Три раскладки в памяти (`Layout = UnionLayout.Overlap | Flat | Boxed`):
  - `Overlap` (по умолчанию): значения перекрываются со значениями, ссылки со ссылками, без аллокаций;
  - `Flat`: у каждого кейса своё поле;
  - `Boxed`: одно поле `object?` без тега, более конкретные кейсы проверяются раньше.
- Разбор смешанных структур (числа и ссылки) по полям через `UnsafeAccessor` (.NET 8+).
- Фоллбек для кейсов, которые нельзя перекрыть: `Fallback = UnionFallback.Field | Boxed`, информационное сообщение `LUNION100` с причиной.
- Настройки на весь проект: MSBuild-свойства `LiteUnionLayout` и `LiteUnionFallback` (атрибут на типе сильнее).
- Интеграция с C# 15 union на .NET 11: `[Union]`, `IUnion`, конструкторы, `Value`, `HasValue`, `TryGetValue`; `switch` разбирает union без боксинга и с проверкой полноты.
- Поддержка структур с указателями (`unsafe`), union'ов внутри union'ов, `Nullable<T>`, кортежей, дженериков.
- Диагностики `LUNION001`–`LUNION007`, `LUNION100`.
- Платформы: `netstandard2.0`, `netstandard2.1`, .NET 8, 9, 10, 11; проверены NativeAOT (Linux x64, .NET 10) и обрезка (`TrimMode=full`).

- Интеграция с C# 15 включается только если `UnionAttribute` и `IUnion` доступны проекту: генератор не принимает за настоящие их внутренние (`internal`) копии из подключённой библиотеки (так делает, например, пакет `StructUnion` для .NET 10).

### Известные ограничения

- Union должен быть обычной `struct`: `record struct` и `class` не поддерживаются.
- Имена элементов кортежа нельзя писать в аргументах атрибута (ограничение C#, `CS8970`).
- На платформах без `UnsafeAccessor` (до .NET 8) смешанные структуры хранятся отдельным полем, без перекрытия.
- Структуры из BCL, у которых справочные сборки скрывают поля, определяются через рефлексию в рантайме компилятора; если тип найти не удалось, он хранится как есть.
- `IComparable` и сериализация (System.Text.Json) не реализованы.
- Запуск NativeAOT на Windows и на настоящем .NET Framework не проверялся.

[1.0.0-rc.1]: https://github.com/alt-stack-lite-dotnet/lite-union/releases/tag/v1.0.0-rc.1
