using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Abstractions;
using Pw.Hub.Infrastructure;
using Pw.Hub.Models;
using Pw.Hub.Pages;
using Pw.Hub.Windows;

namespace Pw.Hub.Services;

public class CharactersLoadService : ICharactersLoadService
{
	private class JsOption
	{
		public string value { get; set; } = string.Empty;

		public string text { get; set; } = string.Empty;
	}

	public async Task LoadForAccountAsync(Account account, AccountPage accountPage, Window owner)
	{
		if (account == null)
		{
			throw new ArgumentNullException("account");
		}
		if (accountPage == null)
		{
			throw new ArgumentNullException("accountPage");
		}
		IBrowser browser = accountPage.Browser;
		IAccountManager accountManager = accountPage.AccountManager;
		ScriptLogWindow log = new ScriptLogWindow("Load Characters", closeWhenEnd: true)
		{
			Owner = owner
		};
		RunAsync();
		log.ShowDialog();
		async Task RunAsync()
		{
			_ = 10;
			try
			{
				log.AppendLog("Switching to account: " + account.Name);
				await accountManager.ChangeAccountAsync(account.Id);
				log.AppendLog("Opening the promotional items page...");
				await browser.NavigateAsync("https://pwonline.ru/promo_items.php");
				await Task.Delay(300);
				await browser.WaitForElementExistsAsync(".js-shard");
				await browser.WaitForElementExistsAsync(".js-char");
				string script = "(function(){var s=document.querySelector('.js-shard');if(!s)return '[]';return JSON.stringify(Array.from(s.options).map(o=>({value:o.value,text:o.text})));})()";
				string text = TrimJs(await browser.ExecuteScriptAsync(script));
				List<JsOption> list = (string.IsNullOrWhiteSpace(text) ? new List<JsOption>() : (JsonSerializer.Deserialize<List<JsOption>>(text) ?? new List<JsOption>()));
				log.AppendLog($"Servers found: {list.Count}");
				List<AccountServer> servers = new List<AccountServer>();
				foreach (JsOption shard in list)
				{
					if (!string.IsNullOrWhiteSpace(shard?.value))
					{
						string script2 = "(function(shard){var sel=document.querySelector('.js-shard');if(!sel)return '[]';sel.value=shard;var e=document.createEvent('HTMLEvents');e.initEvent('change',true,false);sel.dispatchEvent(e);var s2=document.querySelector('.js-char');if(!s2)return '[]';return JSON.stringify(Array.from(s2.options).map(o=>({value:o.value,text:o.text})));})('" + EscapeJs(shard.value) + "')";
						string text2 = TrimJs(await browser.ExecuteScriptAsync(script2));
						List<JsOption> source = (string.IsNullOrWhiteSpace(text2) ? new List<JsOption>() : (JsonSerializer.Deserialize<List<JsOption>>(text2) ?? new List<JsOption>()));
						AccountServer server = new AccountServer
						{
							OptionId = (shard.value ?? string.Empty),
							Name = (shard.text ?? string.Empty),
							Characters = new List<AccountCharacter>(),
							AccountId = account.Id
						};
						server.Characters = source.Select((JsOption ch) => new AccountCharacter
						{
							OptionId = (ch.value ?? string.Empty),
							Name = (ch.text ?? string.Empty),
							ServerId = server.Id
						}).ToList();
						servers.Add(server);
						log.AppendLog($"   {server.Name}: characters — {server.Characters.Count}");
					}
				}
				await using (AppDbContext db = new AppDbContext())
				{
					Account acc = db.Accounts.Include((Account a) => a.Servers).ThenInclude((AccountServer s) => s.Characters).FirstOrDefault((Account a) => a.Id == account.Id);
					if (acc != null)
					{
						foreach (AccountServer server2 in servers)
						{
							List<AccountCharacter> charsToAdd = new List<AccountCharacter>();
							AccountServer existing = acc.Servers.FirstOrDefault((AccountServer s) => s.OptionId == server2.OptionId);
							if (existing != null)
							{
								List<AccountCharacter> list2 = server2.Characters.Where((AccountCharacter ch) => existing.Characters.All((AccountCharacter c) => c.OptionId != ch.OptionId)).ToList();
								foreach (AccountCharacter item in list2)
								{
									item.ServerId = existing.Id;
								}
								charsToAdd.AddRange(list2);
							}
							if (existing == null)
							{
								acc.Servers.Add(server2);
								await db.AddAsync(server2);
								existing = server2;
								charsToAdd.AddRange(server2.Characters);
							}
							if (existing.Characters.Count == 1)
							{
								existing.DefaultCharacterOptionId = existing.Characters.First().OptionId;
							}
							if (charsToAdd.Count > 0)
							{
								await db.AddRangeAsync(charsToAdd);
							}
						}
						db.Update(acc);
						await db.SaveChangesAsync();
					}
				}
				log.MarkCompleted("Done: server and character data were saved.");
			}
			catch (Exception ex)
			{
				log.AppendLog("Error: " + ex.Message);
				log.MarkCompleted("Completed with an error");
			}
		}
	}

	private static string EscapeJs(string s)
	{
		return (s ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"")
			.Replace("\r", string.Empty)
			.Replace("\n", string.Empty);
	}

	private static string TrimJs(string s)
	{
		return (s ?? string.Empty).Replace("\\", string.Empty);
	}
}
