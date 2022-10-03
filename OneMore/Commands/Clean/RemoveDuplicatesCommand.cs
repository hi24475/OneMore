﻿//************************************************************************************************
// Copyright © 2022 Steven M Cohn.  All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Commands
{
	using River.OneMoreAddIn.Commands.Clean;
	using River.OneMoreAddIn.Models;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows.Forms;
	using System.Xml.Linq;
	using Windows.Devices.Scanners;


	/// <summary>
	/// Scan pages looking for and removing duplicates
	/// </summary>
	internal class RemoveDuplicatesCommand : Command
	{
		private sealed class HashNode
		{
			public string PageID;
			public string XmlHash;
			public string TextHash;
			public string Title;
			public string Path;
			public List<HashNode> Siblings = new List<HashNode>();
		}

		private OneNote one;
		private XNamespace ns;
		private readonly MD5CryptoServiceProvider cruncher;
		private readonly List<HashNode> hashes;
		private UI.ProgressDialog progress;


		public RemoveDuplicatesCommand()
		{
			hashes = new List<HashNode>();
			cruncher = new MD5CryptoServiceProvider();
		}


		public override async Task Execute(params object[] args)
		{
			UI.SelectorScope scope;
			IEnumerable<string> books;
			RemoveDuplicatesDialog.DepthKind depth;

			using (var dialog = new RemoveDuplicatesDialog())
			{
				if (dialog.ShowDialog(Owner) != DialogResult.OK)
				{
					return;
				}

				depth = dialog.Depth;
				scope = dialog.Scope;
				books = dialog.SelectedNotebooks;
			}

			var hierarchy = Scan(scope, depth, books);

			await Task.Yield();
		}


		private XElement Scan(
			UI.SelectorScope scope, 
			RemoveDuplicatesDialog.DepthKind depth,
			IEnumerable<string> books)
		{
			logger.StartClock();
			XElement hierarchy = null;
			var count = 0;

			using (progress = new UI.ProgressDialog())
			{
				progress.ShowCancelDialog(Owner, async (dialog, token) =>
				{
					using (one = new OneNote(out _, out ns))
					{
						hierarchy = await BuildHierarchy(scope, books);
						dialog.SetMaximum(hierarchy.Elements().Count());

						hierarchy.Descendants(ns + "Page").ForEach(p =>
						{
							if (token.IsCancellationRequested)
							{
								return;
							}

							// get the XML text rather than the Page so we don't end up
							// converting it back and forth more than once...
							string xml = depth == RemoveDuplicatesDialog.DepthKind.Deep
								? one.GetPageXml(p.Attribute("ID").Value, OneNote.PageDetail.BinaryDataFileType)
								: one.GetPageXml(p.Attribute("ID").Value, OneNote.PageDetail.Basic);

							var node = CalculateHash(xml, depth);
							//logger.WriteLine($"text hash [{node.TextHash}] xml hash [{node.XmlHash}]");

							dialog.SetMessage($"Scanning {node.Title}...");
							dialog.Increment();

							var sibling = hashes.FirstOrDefault(n =>
								n.TextHash == node.TextHash || n.XmlHash == node.XmlHash);

							if (sibling != null)
							{
								node.Path = one.GetPageInfo(node.PageID).Path;
								if (sibling.Path == null)
								{
									sibling.Path = one.GetPageInfo(sibling.PageID).Path;
								}

								//logger.WriteLine($"match [{node.Title}] with [{sibling.Title}]");
								sibling.Siblings.Add(node);
							}
							else
							{
								//logger.WriteLine($"new [{node.Title}]");
								hashes.Add(node);
							}

							count++;
						});
					}

					return true;
				});
			}

			hashes.RemoveAll(n => !n.Siblings.Any());
			logger.WriteTime($"{hashes.Count} duplicate main pages of {count}");

			return hierarchy;
		}


		private async Task<XElement> BuildHierarchy(
			UI.SelectorScope scope, IEnumerable<string> books)
		{
			var hierarchy = new XElement("pages");

			switch (scope)
			{
				case UI.SelectorScope.Section:
					one.GetSection().Descendants(ns + "Page")
						.ForEach(p => hierarchy.Add(p));
					break;

				case UI.SelectorScope.Notebook:
					(await one.GetNotebook(OneNote.Scope.Pages)).Descendants(ns + "Page")
						.ForEach(p => hierarchy.Add(p));
					break;

				case UI.SelectorScope.Notebooks:
					(await one.GetNotebooks(OneNote.Scope.Pages)).Descendants(ns + "Page")
						.ForEach(p => hierarchy.Add(p));
					break;

				default:
					(await BuildSelectedHierarchy(books))
						.ForEach(p => hierarchy.Add(p));
					break;
			}

			return hierarchy;
		}


		private async Task<IEnumerable<XElement>> BuildSelectedHierarchy(IEnumerable<string> books)
		{
			var pages = new List<XElement>();
			foreach (var id in books)
			{
				var book = await one.GetNotebook(id, OneNote.Scope.Pages);
				pages.AddRange(book.Descendants(ns + "Page"));
			}

			return pages;
		}


		private HashNode CalculateHash(string xml, RemoveDuplicatesDialog.DepthKind depth)
		{
			var root = XElement.Parse(xml);
			var page = new Page(root);
			var pageId = page.PageId;

			// EditedByAttributes and the page ID
			root.DescendantsAndSelf().Attributes().Where(a =>
				a.Name.LocalName == "ID"
				|| a.Name.LocalName == "dateTime"
				|| a.Name.LocalName == "callbackID"
				|| a.Name.LocalName == "author"
				|| a.Name.LocalName == "authorInitials"
				|| a.Name.LocalName == "authorResolutionID"
				|| a.Name.LocalName == "lastModifiedBy"
				|| a.Name.LocalName == "lastModifiedByInitials"
				|| a.Name.LocalName == "lastModifiedByResolutionID"
				|| a.Name.LocalName == "creationTime"
				|| a.Name.LocalName == "lastModifiedTime"
				|| a.Name.LocalName == "objectID")
				.Remove();

			var node = new HashNode
			{
				PageID = pageId,
				Title = page.Title,

				TextHash = Convert.ToBase64String(
					cruncher.ComputeHash(Encoding.Default.GetBytes(page.Root.Value)))
			};

			if (depth != RemoveDuplicatesDialog.DepthKind.Basic)
			{
				node.XmlHash = Convert.ToBase64String(
					cruncher.ComputeHash(Encoding.Default.GetBytes(xml)));
			}

			return node;
		}
	}
}
