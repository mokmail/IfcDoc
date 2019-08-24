using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using HtmlAgilityPack;

using IfcDoc.Schema;
using IfcDoc.Schema.DOC;
using BuildingSmart.Serialization.Step;
using BuildingSmart.Serialization.Xml;


namespace IfcDoc
{
	public static class IfcDocUtils
	{
		public static void SaveProject(DocProject project, string filePath)
		{
			project.SortProject();
			DocConstant notdefined = project.Constants.Where(x => string.Compare(x.Name, "NOTDEFINED", true) == 0).Where(x => string.Compare(x.Documentation, "Undefined type.", true) == 0).FirstOrDefault();
			DocConstant userdefined = project.Constants.Where(x => string.Compare(x.Name, "USERDEFINED", true) == 0).Where(x => string.Compare(x.Documentation, "User_defined type.", true) == 0).FirstOrDefault();

			foreach(DocSection section in project.Sections)
			{
				foreach(DocSchema schema in section.Schemas)
				{
					foreach(DocEnumeration enumeration in schema.Types.OfType<DocEnumeration>())
					{
						for(int counter = 0; counter < enumeration.Constants.Count; counter++)
						{
							DocConstant constant = enumeration.Constants[counter];
							if(notdefined != null && string.Compare(notdefined.UniqueId, constant.UniqueId) != 0 && string.Compare(notdefined.Name, constant.Name) == 0 && string.Compare(notdefined.Documentation, constant.Documentation) == 0)
							{
								project.Constants.Remove(constant);
								enumeration.Constants[counter] = notdefined;
							}
							else if(userdefined != null && string.Compare(userdefined.UniqueId, constant.UniqueId) != 0 && string.Compare(userdefined.Name, constant.Name) == 0 && string.Compare(userdefined.Documentation, constant.Documentation) == 0)
							{
								project.Constants.Remove(constant);
								enumeration.Constants[counter] = notdefined;
							}
						}
					}
				}
			}

			DocPropertyConstant other = project.PropertyConstants.Where(x => string.Compare(x.Name, "OTHER", true) == 0).FirstOrDefault();
			DocPropertyConstant none = project.PropertyConstants.Where(x => string.Compare(x.Name, "NONE", true) == 0).FirstOrDefault();
			DocPropertyConstant notknown = project.PropertyConstants.Where(x => string.Compare(x.Name, "NOTKNOWN", true) == 0).FirstOrDefault();
			DocPropertyConstant unset = project.PropertyConstants.Where(x => string.Compare(x.Name, "UNSET", true) == 0).FirstOrDefault();
			DocPropertyConstant notdefinedProperty = project.PropertyConstants.Where(x => string.Compare(x.Name, "NOTDEFINED", true) == 0).FirstOrDefault();
			DocPropertyConstant userdefinedProperty = project.PropertyConstants.Where(x => string.Compare(x.Name, "USERDEFINED", true) == 0).FirstOrDefault();
			
			foreach(DocPropertyEnumeration enumeration in project.PropertyEnumerations)
			{
				for(int counter = 0; counter < enumeration.Constants.Count; counter++)
				{
					DocPropertyConstant constant = enumeration.Constants[counter];
					if (other != null && string.Compare(other.UniqueId, constant.UniqueId) != 0 && string.Compare(other.Name, constant.Name) == 0)
					{
						project.PropertyConstants.Remove(constant);
						enumeration.Constants[counter] = other;
					}
					else if (none != null && string.Compare(none.UniqueId, constant.UniqueId) != 0 && string.Compare(none.Name, constant.Name) == 0)
					{
						project.PropertyConstants.Remove(constant);
						enumeration.Constants[counter] = none;
					}
					else if (notknown != null && string.Compare(notknown.UniqueId, constant.UniqueId) != 0 && string.Compare(notknown.Name, constant.Name) == 0)
					{
						project.PropertyConstants.Remove(constant);
						enumeration.Constants[counter] = notknown;
					}
					else if (unset != null && string.Compare(unset.UniqueId, constant.UniqueId) != 0 && string.Compare(unset.Name, constant.Name) == 0)
					{
						project.PropertyConstants.Remove(constant);
						enumeration.Constants[counter] = unset;
					}
					else if (notdefined != null && string.Compare(notdefinedProperty.UniqueId, constant.UniqueId) != 0 && string.Compare(notdefinedProperty.Name, constant.Name) == 0)
					{
						project.PropertyConstants.Remove(constant);
						enumeration.Constants[counter] = notdefinedProperty;
					}
					else if (userdefinedProperty != null && string.Compare(userdefinedProperty.UniqueId, constant.UniqueId) != 0 && string.Compare(userdefinedProperty.Name, constant.Name) == 0)
					{
						project.PropertyConstants.Remove(constant);
						enumeration.Constants[counter] = userdefinedProperty;
					}
				}
			}
			string ext = System.IO.Path.GetExtension(filePath).ToLower();
			switch (ext)
			{
				case ".ifcdoc":
					using (FileStream streamDoc = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
					{
						StepSerializer formatDoc = new StepSerializer(typeof(DocProject), SchemaDOC.Types, "IFCDOC_12_0", "IfcDoc 12.0", "BuildingSmart IFC Documentation Generator");
						formatDoc.WriteObject(streamDoc, project); // ... specify header...IFCDOC_11_8
					}
					break;

#if MDB
                        case ".mdb":
                            using (FormatMDB format = new FormatMDB(this.m_file, SchemaDOC.Types, this.m_instances))
                            {
                                format.Save();
                            }
                            break;
#endif
				case ".ifcdocxml":
					using (FileStream streamDoc = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
					{
						XmlSerializer formatDoc = new XmlSerializer(typeof(DocProject));

						formatDoc.WriteObject(streamDoc, project); // ... specify header...IFCDOC_11_8
					}
					break;
			}
		}
		public static DocProject LoadFile(string filePath)
		{
			List<object> instances = new List<object>();
			return LoadFile(filePath, out instances);
		}
		public static DocProject LoadFile(string filePath, out List<object> instances)
		{ 
			instances = new List<object>();
			string ext = System.IO.Path.GetExtension(filePath).ToLower();
			string schema = "";
			DocProject project = null;
			switch (ext)
			{
				case ".ifcdoc":
					using (FileStream streamDoc = new FileStream(filePath, FileMode.Open, FileAccess.Read))
					{
						Dictionary<long, object> dictionaryInstances = null;
						StepSerializer formatDoc = new StepSerializer(typeof(DocProject), SchemaDOC.Types);
						project = (DocProject)formatDoc.ReadObject(streamDoc, out dictionaryInstances);
						instances.AddRange(dictionaryInstances.Values);
						schema = formatDoc.Schema;
					}
					break;
				case ".ifcdocxml":
					using (FileStream streamDoc = new FileStream(filePath, FileMode.Open, FileAccess.Read))
					{
						Dictionary<string, object> dictionaryInstances = null;
						XmlSerializer formatDoc = new XmlSerializer(typeof(DocProject));
						project = (DocProject)formatDoc.ReadObject(streamDoc, out dictionaryInstances);
						instances.AddRange(dictionaryInstances.Values);
					}
					break;
				default:
					MessageBox.Show("Unsupported file type " + ext, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					break;
#if MDB
                    case ".mdb":
                        using (FormatMDB format = new FormatMDB(this.m_file, SchemaDOC.Types, this.m_instances))
                        {
                            format.Load();
                        }
                        break;
#endif
			}
			if (project == null)
				return null;

			double schemaVersion = 0;
			if (!string.IsNullOrEmpty(schema))
			{
				string[] fields = schema.Split("_".ToCharArray());
				int i = 0;
				if (fields.Length > 1)
				{
					if (int.TryParse(fields[1], out i))
						schemaVersion = i;
					if (fields.Length > 2 && int.TryParse(fields[2], out i))
						schemaVersion += i / 10.0;
				}
			}
			List<SEntity> listDelete = new List<SEntity>();
			List<DocTemplateDefinition> listTemplate = new List<DocTemplateDefinition>();

			foreach (object o in instances)
			{
				if (o is DocSchema)
				{
					DocSchema docSchema = (DocSchema)o;

					// renumber page references
					foreach (DocPageTarget docTarget in docSchema.PageTargets)
					{
						if (docTarget.Definition != null) // fix it up -- NULL bug from older .ifcdoc files
						{
							int page = docSchema.GetDefinitionPageNumber(docTarget);
							int item = docSchema.GetPageTargetItemNumber(docTarget);
							docTarget.Name = page + "," + item + " " + docTarget.Definition.Name;

							foreach (DocPageSource docSource in docTarget.Sources)
							{
								docSource.Name = docTarget.Name;
							}
						}
					}
				}
				else if (o is DocExchangeDefinition)
				{
					// files before V4.9 had Description field; no longer needed so use regular Documentation field again.
					DocExchangeDefinition docexchange = (DocExchangeDefinition)o;
					if (docexchange._Description != null)
					{
						docexchange.Documentation = docexchange._Description;
						docexchange._Description = null;
					}
				}
				else if (o is DocTemplateDefinition)
				{
					// files before V5.0 had Description field; no longer needed so use regular Documentation field again.
					DocTemplateDefinition doctemplate = (DocTemplateDefinition)o;
					if (doctemplate._Description != null)
					{
						doctemplate.Documentation = doctemplate._Description;
						doctemplate._Description = null;
					}

					listTemplate.Add((DocTemplateDefinition)o);
				}
				else if (o is DocConceptRoot)
				{
					// V12.0: ensure template is defined
					DocConceptRoot docConcRoot = (DocConceptRoot)o;
					if (docConcRoot.ApplicableTemplate == null && docConcRoot.ApplicableEntity != null)
					{
						docConcRoot.ApplicableTemplate = new DocTemplateDefinition();
						docConcRoot.ApplicableTemplate.Type = docConcRoot.ApplicableEntity.Name;
					}
				}
				else if (o is DocTemplateUsage)
				{
					// V12.0: ensure template is defined
					DocTemplateUsage docUsage = (DocTemplateUsage)o;
					if (docUsage.Definition == null)
					{
						docUsage.Definition = new DocTemplateDefinition();
					}
				}
				else if (o is DocLocalization)
				{
					DocLocalization localization = o as DocLocalization;
					if(!string.IsNullOrEmpty(localization.Name))
						localization.Name = localization.Name.Trim();
				}
				// ensure all objects have valid guid
				DocObject docObject = o as DocObject;
				if (docObject != null)
				{
					if (docObject.Uuid == Guid.Empty)
					{
						docObject.Uuid = Guid.NewGuid();
					}
					if (!string.IsNullOrEmpty(docObject.Documentation))
						docObject.Documentation = docObject.Documentation.Trim();

					if (schemaVersion < 12.1)
					{
						DocChangeSet docChangeSet = docObject as DocChangeSet;
						if (docChangeSet != null)
						{
							docChangeSet.ChangesEntities.RemoveAll(isUnchanged);
							docChangeSet.ChangesProperties.RemoveAll(isUnchanged);
							docChangeSet.ChangesQuantities.RemoveAll(isUnchanged);
							docChangeSet.ChangesViews.RemoveAll(isUnchanged);

						}
						else
						{
							if (schemaVersion < 12)
							{
								DocEntity entity = docObject as DocEntity;
								if (entity != null)
								{
									entity.ClearDefaultMember();
								}
							}
						}
					}
				}
			}

			if (project == null)
				return null;

			if (schemaVersion > 0)
			{
				if (schemaVersion < 12.1)
				{
					Dictionary<string, DocPropertyEnumeration> encounteredPropertyEnumerations = new Dictionary<string, DocPropertyEnumeration>();
					foreach (DocSchema docSchema in project.Sections.SelectMany(x => x.Schemas))
						extractListingsV12_1(project, docSchema, encounteredPropertyEnumerations);
				}
				if (schemaVersion < 12.2)
				{
					foreach (IDocumentation obj in instances.OfType<IDocumentation>().Where(x=> !string.IsNullOrEmpty(x.Documentation)))
						obj.Documentation = convertToMarkdown(obj.Documentation);
				}
			}
			foreach (DocModelView docModelView in project.ModelViews)
			{
				// sort alphabetically (V11.3+)
				docModelView.SortConceptRoots();
			}

			// upgrade to Publications (V9.6)
			if (project.Annotations.Count == 4)
			{
				project.Publications.Clear();

				DocAnnotation docCover = project.Annotations[0];
				DocAnnotation docContents = project.Annotations[1];
				DocAnnotation docForeword = project.Annotations[2];
				DocAnnotation docIntro = project.Annotations[3];

				DocPublication docPub = new DocPublication();
				docPub.Name = "Default";
				docPub.Documentation = docCover.Documentation;
				docPub.Owner = docCover.Owner;
				docPub.Author = docCover.Author;
				docPub.Code = docCover.Code;
				docPub.Copyright = docCover.Copyright;
				docPub.Status = docCover.Status;
				docPub.Version = docCover.Version;

				docPub.Annotations.Add(docForeword);
				docPub.Annotations.Add(docIntro);

				project.Publications.Add(docPub);

				docCover.Delete();
				docContents.Delete();
				project.Annotations.Clear();
			}
			project.SortProject();
			return project;
		}

		
		private static bool isUnchanged(DocChangeAction docChangeAction)
		{
			docChangeAction.Changes.RemoveAll(isUnchanged);
			if (docChangeAction.Changes.Count ==  0 && docChangeAction.Action == DocChangeActionEnum.NOCHANGE && !docChangeAction.ImpactXML && !docChangeAction.ImpactSPF)
				return true;
			return false;
		}
		private static void extractListingsV12_1(DocProject project, DocSchema schema, Dictionary<string, DocPropertyEnumeration> encounteredPropertyEnumerations)
		{
			foreach(DocPropertyEnumeration enumeration in schema.PropertyEnumerations)
			{
				if (encounteredPropertyEnumerations.ContainsKey(enumeration.Name))
					continue;
				project.PropertyEnumerations.Add(enumeration);
				encounteredPropertyEnumerations[enumeration.Name] = enumeration;
				foreach(DocPropertyConstant constant in enumeration.Constants)
				{
					constant.Name = constant.Name.Trim();
					if (!project.PropertyConstants.Contains(constant))
						project.PropertyConstants.Add(constant);
				}
			}
			foreach (DocType t in schema.Types)
			{
				DocEnumeration enumeration = t as DocEnumeration;
				if (enumeration != null)
				{
					foreach (DocConstant constant in enumeration.Constants)
					{
						if (!project.Constants.Contains(constant)) 
							project.Constants.Add(constant);
					}
				}
			}
			foreach (DocProperty property in schema.PropertySets.SelectMany(x=>x.Properties))
				extractListings(project, property, encounteredPropertyEnumerations); //listings
		
			foreach (DocQuantity quantity in schema.QuantitySets.SelectMany(x => x.Quantities))
				project.Quantities.Add(quantity);
		}

		private static void extractListings(DocProject project, DocProperty property, Dictionary<string, DocPropertyEnumeration> encounteredPropertyEnumerations)
		{
			project.Properties.Add(property);

			if (string.IsNullOrEmpty(property.SecondaryDataType))
				property.SecondaryDataType = null;
			else if (property.PropertyType == DocPropertyTemplateTypeEnum.P_ENUMERATEDVALUE && property.Enumeration == null)
			{
				string[] fields = property.SecondaryDataType.Split(":".ToCharArray());
				if(fields.Length == 1)
				{
					string name = fields[0];
					foreach(DocPropertyEnumeration docEnumeration in project.PropertyEnumerations)
					{
						if(string.Compare(name, docEnumeration.Name) == 0)
						{
							property.Enumeration = docEnumeration;
							property.SecondaryDataType = null;
							break;
						}
					}
				}
				else if (fields.Length == 2)
				{
					string name = fields[0];
					DocPropertyEnumeration propertyEnumeration = null;
					if (encounteredPropertyEnumerations.TryGetValue(name, out propertyEnumeration))
					{
						property.SecondaryDataType = null;
						property.Enumeration = propertyEnumeration;
					}
					else
					{
						foreach (DocPropertyEnumeration docEnumeration in project.PropertyEnumerations)
						{
							if (string.Compare(name, docEnumeration.Name) == 0)
							{
								property.Enumeration = docEnumeration;
								property.SecondaryDataType = null;
								break;
							}
						}
						if (property.Enumeration == null)
						{
							property.Enumeration = new DocPropertyEnumeration() { Name = name };
							project.PropertyEnumerations.Add(property.Enumeration = property.Enumeration);
							encounteredPropertyEnumerations[name] = property.Enumeration;
							foreach (string str in fields[1].Split(",".ToCharArray()))
							{
								string constantName = str.Trim();
								DocPropertyConstant constant = null;
								foreach (DocPropertyConstant docConstant in project.PropertyConstants)
								{
									if (string.Compare(docConstant.Name, constantName) == 0)
									{
										constant = docConstant;
										break;
									}
								}
								if (constant == null)
								{
									constant = new DocPropertyConstant() { Name = constantName };
									project.PropertyConstants.Add(constant);
								}
								property.Enumeration.Constants.Add(constant);
							}
							property.SecondaryDataType = null;
						}
					}
				}
			}

			foreach (DocProperty element in property.Elements)
				extractListings(project, element, encounteredPropertyEnumerations);
		}


		private static string openingTag(HtmlNode node)
		{
			string result = "<" + node.Name;
			foreach (HtmlAttribute attribute in node.Attributes)
				result += " " + attribute.Name + "=\"" + attribute.Value;
			return result + ">";
		}
		private static string convertToMarkdown(string html)
		{
			HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
			string documentation = Regex.Replace(html, @"<li>&quot;\s+", "<li>&quot;").Replace("<br/>\r\n", "<br/>");
			documentation = DocumentationISO.UpdateNumbering(documentation, new List<DocumentationISO.ContentRef>(), new List<DocumentationISO.ContentRef>(), null);
			doc.LoadHtml(documentation);
			HtmlNode node = doc.DocumentNode;
			return convertToMarkdown(node, new ConvertMarkdownOptions());
		}
		private static string convertToMarkdown(HtmlNode node, ConvertMarkdownOptions options)
		{
			ConvertMarkdownOptions childOptions = new ConvertMarkdownOptions(options);
			if (node.NodeType == HtmlNodeType.Text)
			{
				string text = node.InnerText.Replace("&quot;", "\"");
				if (!options.m_NestsCode)
				{
					Regex regex = new Regex("[ ]{2,}", RegexOptions.None);
					text = regex.Replace(Regex.Replace(text, @"\n|\r", " "), " ");

				}
				string escaped = "";
				foreach (char c in text)
				{
					if (c == '*')// || c == '\\' || c == '`' || c == '_' || c == '{' || c == '}' || c == '[' || c == ']' || c == '(' || c == ')' || c == '#' || c == '+' || c == '-' || c == '!' || c == '.')
						escaped += '\\';
					escaped += c;
				}
				return escaped;
			}

			if (node.NodeType == HtmlNodeType.Comment)
				return "";

			string result = "", suffix = "";
			if (node.NodeType == HtmlNodeType.Document)
			{
				HtmlNode firstChild = node.FirstChild;
				if (firstChild != null && string.Compare(firstChild.Name, "tr", true) == 0)
				{
					return node.OuterHtml;
				}
			}
			if (node.NodeType == HtmlNodeType.Element)
			{
				if (node.HasAttributes)
				{
					string _class = "", id = "";
					foreach (HtmlAttribute attribute in node.Attributes)
					{
						if (string.Compare(attribute.Name, "Class", true) == 0)
							_class = attribute.Value;
						else if (string.Compare(attribute.Name, "id", true) == 0)
							id = attribute.Value;
					}
					if (!string.IsNullOrEmpty(id))
						result += "{#" + id + (string.IsNullOrEmpty(_class) ? "}\r\n" : " ." + _class + "}\r\n");
					else if (!string.IsNullOrEmpty(_class))
						result += "{ ." + _class + "}\r\n";

				}
				string name = node.Name;
				if (string.IsNullOrEmpty(name))
					return node.OuterHtml;
				if (string.Compare(name, "a", true) == 0)
				{
					result = "";
					string href = "", target = "", _class = "", id = "";
					foreach (HtmlAttribute attribute in node.Attributes)
					{
						name = attribute.Name;
						if (string.Compare(name, "href", true) == 0)
							href = attribute.Value;
						else if (string.Compare(name, "target", true) == 0)
							target = attribute.Value;
						else if (string.Compare(name, "class", true) == 0)
							_class = attribute.Value;
						else if (string.Compare(name, "id", true) == 0)
							id = attribute.Value;
						else
						{
							suffix = "";
						}
					}
					if (!string.IsNullOrEmpty(href))
					{
						result += "[";
						suffix = "](" + href + ")";
					}
					if (!string.IsNullOrEmpty(_class) || !string.IsNullOrEmpty(id) || !string.IsNullOrEmpty(target))
						suffix += "{" + (string.IsNullOrEmpty(id) ? "" : "#" + id) + (string.IsNullOrEmpty(_class) ? "" : " ." + _class) + (string.IsNullOrEmpty(target) ? "" : " target=\"" + target + "\"") + "}";
				}
				else if (string.Compare(name, "b", true) == 0 || string.Compare(name, "strong", true) == 0)
				{
					result += "**";
					suffix = "**";
				}
				else if (string.Compare(name, "blockquote", true) == 0)
				{
					if (node.ChildNodes.Count == 1)
					{
						if (string.Compare(node.FirstChild.Name, "code", true) == 0)
							return convertToMarkdown(node.FirstChild, new ConvertMarkdownOptions(childOptions) { m_NestsCode = true });
						if (string.Compare(node.FirstChild.Name, "table", true) == 0)
							return convertToMarkdown(node.FirstChild, childOptions);
					}
					result += "> ";
					suffix = "\r\n\r\n";
				}
				else if (string.Compare(name, "big", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "br", true) == 0)
					suffix = "  \r\n";
				else if (string.Compare(name, "code", true) == 0)
				{
					result += "\r\n~~~\r\n";
					suffix = "\r\n~~~\r\n";
					childOptions.m_NestsCode = true;
				}
				else if (string.Compare(name, "div", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "em", true) == 0 || string.Compare(name, "i", true) == 0)
				{
					result += "_";
					suffix = "_";
				}
				else if (string.Compare(name, "epm-html", true) == 0)
				{
					//ignore
				}
				else if (string.Compare(name, "font", true) == 0)
					return node.OuterHtml;
				else if (name.Length == 2 && name[0] == 'h' && char.IsDigit(name[1]))
				{
					int count = 0;
					if (int.TryParse(name.Substring(1), out count))
					{
						if (count == 1)
							suffix = "\r\n=======\r\n";
						else
						{
							result += new String('#', count) + " ";
							suffix = "\r\n";
						}
					}
				}
				else if (string.Compare(name, "hr", true) == 0)
					return "___" + "\r\n";
				else if (string.Compare(name, "img", true) == 0)
				{
					string alt = "Image", source = "";
					foreach (HtmlAttribute attribute in node.Attributes)
					{
						if (string.Compare(attribute.Name, "src", true) == 0)
							source = attribute.Value;
						else if (string.Compare(attribute.Name, "alt", true) == 0)
							alt = attribute.Value;
						else
						{
							suffix = "";
						}

						if (!string.IsNullOrEmpty(source))
							return "![" + alt + "](" + source + ")\r\n";
					}
				}
				else if (string.Compare(name, "li", true) == 0 || string.Compare(name, "td", true) == 0 || string.Compare(name, "th", true) == 0)
				{
					//handled in parent
				}
				else if (string.Compare(name, "ol", true) == 0)
					return convertListToMarkdown(node, childOptions, true) + suffix;
				else if (string.Compare(name, "noscript", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "p", true) == 0)
					suffix = "\r\n\r\n";
				else if (string.Compare(name, "pre", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "small", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "span", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "strike", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "sub", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "sup", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "super", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "table", true) == 0)
				{
					return convertTable(node, options);
				}
				else if (string.Compare(name, "u", true) == 0)
					return node.OuterHtml;
				else if (string.Compare(name, "ul", true) == 0)
					return convertListToMarkdown(node, childOptions, false);
				else
					return node.OuterHtml;
			}

			if (node.ChildNodes.Count == 0)
				return result + node.InnerText + suffix;

			string inner = "";
			foreach (HtmlNode n in node.ChildNodes)
			{
				if (string.Compare(n.Name, "br", true) == 0)
					inner += "  \r\n";
				else
				{
					string str = convertToMarkdown(n, childOptions);
					if (!string.IsNullOrWhiteSpace(str))
						inner += str;
				}
			}

			if (childOptions.m_NestsCode)
				inner = inner.Trim("\r\n".ToCharArray());
			else
				inner = inner.Trim();
			return result + inner + suffix;
		}

		private static string convertTable(HtmlNode node, ConvertMarkdownOptions options)
		{
			if(node.ChildNodes.Count == 2)
			{

			}
			return node.OuterHtml + "\r\n\r\n";
			//List<string> headerRow = new List<string>();
			//List<string> delimiterRow = new List<string>();
			//string body = "";
			//if (node.ChildNodes.Count > 0)
			//{
			//	foreach (HtmlNode tr in node.FirstChild.ChildNodes)
			//	{
			//		extractTableHeader(tr, childOptions, ref headerRow, ref delimiterRow);
			//	}

			//	result += string.Join(" | ", headerRow) + "\r\n";
			//	result += string.Join(" | ", delimiterRow) + "\r\n";
			//	foreach (HtmlNode hn in node.ChildNodes.Skip(1))
			//	{
			//		List<string> row = new List<string>();
			//		if (string.Compare(hn.Name, "tr", true) == 0)
			//		{
			//			extractTableRow(hn, childOptions, ref row);
			//			if (row.Count > 0)
			//				body +=  string.Join(" | ", row) + "\r\n";
			//		}
			//		else if (string.Compare(hn.Name, "tbody", true) == 0)
			//		{
			//			foreach (HtmlNode tr in hn.ChildNodes)
			//			{
			//				if (string.Compare(tr.Name, "tr", true) == 0)
			//				{
			//					extractTableRow(tr, options, ref row);
			//					if (row.Count > 0)
			//						body += "| " + string.Join(" | ", row) + " |\r\n";
			//				}
			//			}
			//		}
			//	}
			//	return result + body + "\r\n\r\n";
			//}
		}
		private static void extractTableHeader(HtmlNode node, ConvertMarkdownOptions options, ref List<string> headerRow, ref List<string> delimiterRow)
		{
			if (string.Compare(node.Name, "td", true) == 0 || string.Compare(node.Name, "th", true) == 0)
			{
				string headerValue = convertToMarkdown(node, options).Trim();
				headerRow.Add(headerValue);
				delimiterRow.Add(new String('-', Math.Max(3, headerValue.Length)));
				return;
			}
			foreach (HtmlNode n in node.ChildNodes)
			{
				extractTableHeader(n, options, ref headerRow, ref delimiterRow);
			}
		}
		private static void extractTableRow(HtmlNode node, ConvertMarkdownOptions options, ref List<string> row)
		{
			if (string.Compare(node.Name, "td", true) == 0)
			{
				string value = convertToMarkdown(node, options).Trim();
				if (!string.IsNullOrEmpty(value))
					row.Add(value);
				return;
			}
			foreach (HtmlNode n in node.ChildNodes)
			{
				extractTableRow(n, options, ref row);
			}
		}
		private static string convertListToMarkdown(HtmlNode node, ConvertMarkdownOptions options, bool isOrdered)
		{
			string result = "";
			int count = 1;
			foreach (HtmlNode hn in node.ChildNodes)
			{
				string listValue = convertToMarkdown(hn, options).Trim();
				if (!string.IsNullOrEmpty(listValue))
				{
					if (listValue.Contains("\r\n"))
					{
						result = "";
						break;
					}

					result += (isOrdered ? count + ". " : "* ") + listValue + "\r\n";
					count++;
				}
			}
			if (!string.IsNullOrEmpty(result)) // simple list
				return result + "\r\n";

			//result += openingTag(node) + "\r\n";
			//foreach (HtmlNode hn in node.ChildNodes)
			//{
			//	string listValue = convertToMarkdown(hn, true).Trim();
			//	if (!string.IsNullOrEmpty(listValue))
			//		result += openingTag(hn) + listValue + "\r\n</" + hn.Name + ">\r\n";
			//}
			//return result + "\r\n</" + node.Name + ">\r\n";
			return node.OuterHtml;
		}


		private class ConvertMarkdownOptions
		{
			internal bool m_NestsCode = false;
			internal bool m_CollapseLines = false;

			internal ConvertMarkdownOptions() { }
			internal ConvertMarkdownOptions(ConvertMarkdownOptions config)
			{
				m_NestsCode = config.m_NestsCode;
				m_CollapseLines = config.m_CollapseLines;
			}

		}
	}
}
