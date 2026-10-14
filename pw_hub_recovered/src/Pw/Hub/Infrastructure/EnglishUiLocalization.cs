using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Pw.Hub.Infrastructure;

public static class EnglishUiLocalization
{
	private static readonly Dictionary<string, string> Translations = new(StringComparer.Ordinal)
	{
		["Свернуть"] = "Minimize",
		["Развернуть"] = "Maximize",
		["Закрыть"] = "Close",
		["Назад"] = "Back",
		["Обновить"] = "Refresh",
		["Вперёд"] = "Forward",
		["Адрес (только pwonline.ru)"] = "Address (pwonline.ru only)",
		["Библиотека модулей"] = "Module Library",
		["Профиль"] = "Profile",
		["БЫСТРЫЙ ЗАПУСК"] = "QUICK LAUNCH",
		["▶ Запустить"] = "▶ Run",
		["ОТРЯДЫ И АККАУНТЫ"] = "SQUADS AND ACCOUNTS",
		["➕ Добавить отряд"] = "➕ Add Squad",
		["👤 Добавить аккаунт"] = "👤 Add Account",
		["✏ Редактировать отряд"] = "✏ Edit Squad",
		["✏ Редактировать аккаунт"] = "✏ Edit Account",
		["⬇ Загрузить персонажей"] = "⬇ Load Characters",
		["🗑 Удалить"] = "🗑 Delete",
		["Нет аккаунтов в этом отряде"] = "This squad has no accounts",
		["Нет отрядов"] = "No squads",
		["Создайте отряд, затем добавьте аккаунты"] = "Create a squad, then add accounts",
		["Готово"] = "Ready",
		["Доступны обновления модулей"] = "Module updates are available",
		["Главная (pwonline.ru)"] = "Home (pwonline.ru)",
		["🏠 Главная"] = "🏠 Home",
		["Перевод (promo_items)"] = "Transfer (promo_items)",
		["💱 Перевод"] = "💱 Transfer",
		["МДМ (chests2)"] = "MDM (chests2)",
		["🎁 МДМ"] = "🎁 MDM",
		["Выберите аккаунт"] = "Select an account",
		["Создать аккаунт"] = "Create Account",
		["👤 Создание аккаунта"] = "👤 Create Account",
		["Название аккаунта"] = "Account name",
		["Отмена"] = "Cancel",
		["Создать"] = "Create",
		["Создать отряд"] = "Create Squad",
		["📁 Создание отряда"] = "📁 Create Squad",
		["Название отряда"] = "Squad name",
		["Редактировать аккаунт"] = "Edit Account",
		["✏ Редактирование аккаунта"] = "✏ Edit Account",
		["ПЕРСОНАЖ ПО УМОЛЧАНИЮ ПО СЕРВЕРАМ"] = "DEFAULT CHARACTER BY SERVER",
		["Сохранить"] = "Save",
		["Редактировать отряд"] = "Edit Squad",
		["✏ Редактирование отряда"] = "✏ Edit Squad",
		["Настройки AI"] = "AI Settings",
		["Настройка AI подключения"] = "AI Connection Settings",
		["Модель"] = "Model",
		["Lua — Переменные на точке останова"] = "Lua — Variables at Breakpoint",
		["Globals (частично)"] = "Globals (partial)",
		["Продолжить"] = "Continue",
		["Полные изменения"] = "Full Changes",
		["Полный diff изменений"] = "Full Change Diff",
		["Вход в PW Modules"] = "Sign In to PW Modules",
		["🔐 Авторизация"] = "🔐 Sign In",
		["Подключение к библиотеке модулей"] = "Connect to the module library",
		["Имя пользователя"] = "Username",
		["Пароль"] = "Password",
		["Запомнить меня"] = "Remember me",
		["Регистрация"] = "Register",
		["Войти"] = "Sign In",
		["Логи (Debug)"] = "Logs (Debug)",
		["Уровень:"] = "Level:",
		["Категория:"] = "Category:",
		["Автопрокрутка"] = "Auto-scroll",
		["Время"] = "Time",
		["Уровень"] = "Level",
		["Категория"] = "Category",
		["Сообщение"] = "Message",
		["▶ Отладка"] = "▶ Debug",
		["⏹ Остановить"] = "⏹ Stop",
		["🗑 Очистить вывод"] = "🗑 Clear Output",
		["✖ Закрыть"] = "✖ Close",
		["⚫ F9 — поставить/снять брейкпоинт на текущей строке"] = "⚫ F9 — toggle a breakpoint on the current line",
		["📝 Редактор кода Lua"] = "📝 Lua Code Editor",
		["📟 Консоль вывода (Print)"] = "📟 Output Console (Print)",
		["🤖 AI помощник"] = "🤖 AI Assistant",
		["Новая сессия"] = "New Session",
		["Показать полные изменения"] = "Show Full Changes",
		["Предпросмотр изменений"] = "Change Preview",
		["⤢ перетяните разделители, чтобы изменить высоту областей"] = "⤢ drag the dividers to resize the panels",
		["AI будет сам вносить изменения в код"] = "AI will apply changes to the code automatically",
		["Отправить"] = "Send",
		["Вставить изменения"] = "Apply Changes",
		["⚙ Параметры модуля"] = "⚙ Module Parameters",
		["Запустить"] = "Run",
		["Редактор модуля (API)"] = "Module Editor (API)",
		["📦 Редактор модуля (API)"] = "📦 Module Editor (API)",
		["Основная информация"] = "General Information",
		["Название"] = "Name",
		["Версия"] = "Version",
		["Описание (Markdown)"] = "Description (Markdown)",
		["Показывать превью"] = "Show preview",
		["Форматирование:"] = "Formatting:",
		["Жирный"] = "Bold",
		["Курсив"] = "Italic",
		["Заголовок"] = "Heading",
		["Список"] = "List",
		["Код"] = "Code",
		["🤖 Сгенерировать описание"] = "🤖 Generate Description",
		["Сгенерировать описание модуля с помощью AI"] = "Generate the module description with AI",
		["✏ Редактор"] = "✏ Editor",
		["👁 Превью"] = "👁 Preview",
		["Lua скрипт"] = "Lua Script",
		["Редактирование скрипта выполняется во встроенном Lua-редакторе"] = "Edit the script in the built-in Lua editor",
		["🧩 Редактор скрипта"] = "🧩 Script Editor",
		["Входные параметры"] = "Input Parameters",
		["➕ Добавить"] = "➕ Add",
		["Список вариантов через запятую или точку с запятой"] = "Options separated by commas or semicolons",
		["📚 Библиотека модулей"] = "📚 Module Library",
		["🔍 Найти"] = "🔍 Search",
		["Теги через запятую"] = "Tags separated by commas",
		["Сортировка: имя"] = "Sort: name",
		["Сортировка: установки"] = "Sort: installs",
		["Сортировка: запуски"] = "Sort: runs",
		["Сортировка: популярные (30д)"] = "Sort: popular (30d)",
		["По возрастанию"] = "Ascending",
		["По убыванию"] = "Descending",
		["⟳ Обновить все"] = "⟳ Update All",
		["Модуль"] = "Module",
		["⬇ Установить"] = "⬇ Install",
		["⟳ Обновить"] = "⟳ Update",
		["👨‍💻 Режим разработчика"] = "👨‍💻 Developer Mode",
		["✏ Редактировать"] = "✏ Edit",
		["Редактор Lua"] = "Lua Editor",
		["Откроется существующее окно редактора Lua (логика будет перенесена в VM в следующих итерациях)"] = "The existing Lua editor window will open",
		["Сохранить имя"] = "Save Username",
		["Смена пароля"] = "Change Password",
		["Текущий пароль"] = "Current password",
		["Новый пароль"] = "New password",
		["Повторите новый пароль"] = "Repeat new password",
		["Сменить пароль"] = "Change Password",
		["Привязать Telegram"] = "Link Telegram",
		["Отвязать"] = "Unlink",
		["Сменить аккаунт"] = "Switch Account",
		["Выполнение модуля"] = "Module Execution",
		["📟 Консоль вывода"] = "📟 Output Console",
		["✔ Закрыть"] = "✔ Close",
		["Обновление"] = "Update",
		["Загрузка обновления..."] = "Downloading update...",
		["Подготовка..."] = "Preparing..."
	};

	private static bool _enabled;

	public static void Enable()
	{
		if (_enabled)
		{
			return;
		}

		_enabled = true;
		EventManager.RegisterClassHandler(
			typeof(FrameworkElement),
			FrameworkElement.LoadedEvent,
			new RoutedEventHandler(OnElementLoaded),
			true);
	}

	public static string Translate(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return value ?? string.Empty;
		}

		string trimmed = value.Trim();
		Match breakpointCount = Regex.Match(trimmed, @"^\((\d+) точек\)$");
		if (breakpointCount.Success)
		{
			return value.Replace(trimmed, $"({breakpointCount.Groups[1].Value} breakpoints)", StringComparison.Ordinal);
		}
		if (!Translations.TryGetValue(trimmed, out string? translated))
		{
			return value;
		}

		int start = value.IndexOf(trimmed, StringComparison.Ordinal);
		return value[..start] + translated + value[(start + trimmed.Length)..];
	}

	private static void OnElementLoaded(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement element)
		{
			return;
		}

		if (element is Window window && !BindingOperations.IsDataBound(window, Window.TitleProperty))
		{
			window.Title = Translate(window.Title);
		}
		if (element is TextBlock textBlock && !BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty))
		{
			textBlock.Text = Translate(textBlock.Text);
		}
		if (element is ContentControl contentControl
			&& !BindingOperations.IsDataBound(contentControl, ContentControl.ContentProperty)
			&& contentControl.Content is string content)
		{
			contentControl.Content = Translate(content);
		}
		if (element is HeaderedContentControl headeredContent
			&& !BindingOperations.IsDataBound(headeredContent, HeaderedContentControl.HeaderProperty)
			&& headeredContent.Header is string header)
		{
			headeredContent.Header = Translate(header);
		}
		if (element is HeaderedItemsControl headeredItems
			&& !BindingOperations.IsDataBound(headeredItems, HeaderedItemsControl.HeaderProperty)
			&& headeredItems.Header is string itemsHeader)
		{
			headeredItems.Header = Translate(itemsHeader);
		}
		if (!BindingOperations.IsDataBound(element, FrameworkElement.ToolTipProperty)
			&& element.ToolTip is string toolTip)
		{
			element.ToolTip = Translate(toolTip);
		}
		if (element is DataGrid dataGrid)
		{
			foreach (DataGridColumn column in dataGrid.Columns)
			{
				if (column.Header is string columnHeader)
				{
					column.Header = Translate(columnHeader);
				}
			}
		}
	}
}
