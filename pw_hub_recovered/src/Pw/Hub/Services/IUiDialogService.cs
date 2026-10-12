using System.Collections.Generic;
using System.Windows;
using Pw.Hub.Windows;

namespace Pw.Hub.Services;

public interface IUiDialogService
{
	void Alert(string message, string caption = "PW Hub");

	bool Confirm(string message, string caption = "Подтверждение");

	string? Prompt(string prompt, string caption = "Ввод", string? defaultValue = null);

	Dictionary<string, object>? AskRunArguments(IList<InputDefinitionDto> inputs);

	Dictionary<string, object>? AskRunArguments(IList<InputDefinitionDto> inputs, ref ModuleArgsWindow window, Window owner);
}
