using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IfcDoc.Schema.DOC;

namespace IfcDoc.Formats
{
	/// <summary>
	/// Capture link to table or figure
	/// </summary>
	public struct ContentRef
	{
		public string Caption; // caption to be displayed in table of contents
		public DocObject Page; // relative link to reference

		public ContentRef(string caption, DocObject page)
		{
			this.Caption = caption;
			this.Page = page;
		}

		/// <summary>
		/// Updates content containing figure references
		/// </summary>
		/// <param name="html">Content to parse</param>
		/// <param name="figurenumber">Last figure number; returns updated last figure number</param>
		/// <param name="tablenumber">Last table number; returns updated last table number</param>
		/// <returns>Updated content</returns>
		public static string UpdateNumbering(string html, List<ContentRef> listFigures, List<ContentRef> listTables, DocObject target)
		{
			if (string.IsNullOrEmpty(html))
				return null;

			html = UpdateNumbering(html, "Figure", "figure", listFigures, target);
			html = UpdateNumbering(html, "Table", "table", listTables, target);
			return html;
		}

		/// <summary>
		/// Updates numbering of figures or tables within HTML text
		/// </summary>
		/// <param name="html">The existing HTML</param>
		/// <param name="tag">The caption to find -- either 'Figure' or 'Table'</param>
		/// <param name="style">The style to find -- either 'figure' or 'table'</param>
		/// <param name="listRef">List of items where numbering begins and items are added.</param>
		/// <returns>The new HTML with figures or numbers updated</returns>
		private static string UpdateNumbering(string html, string tag, string style, List<ContentRef> listRef, DocObject target)
		{
			Dictionary<int, int> list = new Dictionary<int, int>();

			html = html.Replace("—", "&mdash;");
			// first get numbers of existing figures (must be unique within page)
			int index = 0;
			for (int count = 0; ; count++)
			{
				index = html.IndexOf("<p class=\"" + style + "\">", index);
				if (index == -1)
					break;

				// <p class="figure">Figure 278 &mdash; Circle geometry</p>
				// <p class="table">Table 278 &mdash; Circle geometry</p>

				// get the existing figure number, add it to list
				int head = index + 13 + tag.Length * 2; // was 25
				int tail = html.IndexOf(" &mdash;", index);
				if (tail > head)
				{
					string exist = html.Substring(head, tail - head);
					int result = 0;
					if (Int32.TryParse(exist, out result))
					{
						list[result] = listRef.Count + 1;


						int endcaption = html.IndexOf("<", tail);
						string figuretext = html.Substring(tail + 9, endcaption - tail - 9);

						listRef.Add(new ContentRef(figuretext, target));
					}
					else
						System.Diagnostics.Debug.WriteLine(target == null ? "" : target.Name + "invalid " + tag + " numbering :" + exist);
				}

				index++;
			}

			if (list.Count > 0)
			{
				// renumber in two phases (to avoid renumbering same)

				// now renumber
				foreach (KeyValuePair<int, int> pair in list)
				{
					string captionold = tag + " " + pair.Key;// +" ";
					string captionnew = tag + "#" + pair.Value;// +" ";

					// handle cases of space, comma, and period following figure reference
					html = html.Replace(captionold + " ", captionnew + " ");
					html = html.Replace(captionold + ",", captionnew + ",");
					html = html.Replace(captionold + ".", captionnew + ".");
				}

				// then replace all
				html = html.Replace(tag + "#", tag + " ");
			}

			//itemnumber += list.Count;

			return html;
		}
	}
}
