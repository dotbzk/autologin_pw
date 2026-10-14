using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pw.Hub.Models;

public class AccountServer
{
	public string Id { get; set; } = Guid.NewGuid().ToString();

	public string OptionId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? DefaultCharacterOptionId { get; set; }

	public List<AccountCharacter> Characters { get; set; } = new List<AccountCharacter>();

	public Account? Account { get; set; }

	public string? AccountId { get; set; }

	[NotMapped]
	public List<AccountCharacter> CharactersWithPlaceholder
	{
		get
		{
			List<AccountCharacter> list = new List<AccountCharacter>
			{
				new AccountCharacter
				{
					OptionId = null,
					Name = "— not selected —"
				}
			};
			if (Characters != null && Characters.Count > 0)
			{
				list.AddRange(Characters);
			}
			return list;
		}
	}
}
