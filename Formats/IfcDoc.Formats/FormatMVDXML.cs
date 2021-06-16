using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IfcDoc.Schema.DOC;
using IfcDoc.Schema.MVD;

namespace IfcDoc.Format.MVDXML
{
	public class FormatMVDXML
	{
		public static void Load(DocProject docProject, string filename)
		{
			for (int iNS = 0; iNS < mvdXML.Namespaces.Length; iNS++)
			{
				string xmlns = mvdXML.Namespaces[iNS];
				mvdXML mvd = null;
				BuildingSmart.Serialization.Xml.XmlSerializer xmlSerializer = new BuildingSmart.Serialization.Xml.XmlSerializer(typeof(mvdXML));
				using (System.IO.FileStream streamSource = new System.IO.FileStream(filename, System.IO.FileMode.Open))
				{
					mvd = xmlSerializer.ReadObject(streamSource) as mvdXML;
				}
				if (mvd != null)
				{
					try
					{
						ImportMvd(mvd, docProject, filename);
						break;
					}
					catch (InvalidOperationException xx)
					{
						// keep going until successful

						if (iNS == mvdXML.Namespaces.Length - 1)
						{
							throw new InvalidOperationException("The file is not of a supported format (mvdXML 1.0 or mvdXML 1.1).");
						}
					}
					catch (Exception xx)
					{
						xmlns = null;
					}
				}
			}
		}


		public static void ImportMvd(mvdXML mvd, DocProject docProject, string filepath)
		{
			List<DocTemplateDefinition> listPrivateTemplateRoots = new List<DocTemplateDefinition>();

			if (mvd.Templates != null)
			{
				Dictionary<EntityRule, DocModelRuleEntity> fixups = new Dictionary<EntityRule, DocModelRuleEntity>();
				foreach (ConceptTemplate mvdTemplate in mvd.Templates)
				{
					DocTemplateDefinition docDef = docProject.GetTemplate(mvdTemplate.Uuid);
					if (docDef == null)
					{
						docDef = new DocTemplateDefinition();
						docProject.Templates.Add(docDef);
					}

					ImportMvdTemplate(mvdTemplate, docDef, fixups);

					if (mvdTemplate.Name.StartsWith("_"))
					{
						// hidden -- treat as private template
						listPrivateTemplateRoots.Add(docDef);
					}
				}

				foreach (EntityRule er in fixups.Keys)
				{
					DocModelRuleEntity docEntityRule = fixups[er];
					if (er.References != null)
					{
						foreach (TemplateRef tr in er.References.Template)
						{
							DocTemplateDefinition dtd = docProject.GetTemplate(tr.Ref);
							if (dtd != null)
							{
								docEntityRule.References.Add(dtd);
							}
						}
					}
				}
			}

			if (mvd.Views != null)
			{
				foreach (ModelView mvdView in mvd.Views)
				{
					DocModelView docView = docProject.GetView(mvdView.Uuid);
					if (docView == null)
					{
						docView = new DocModelView();
						docProject.ModelViews.Add(docView);
					}

					ImportMvdView(mvdView, docView, docProject, filepath);
				}
			}

			foreach (DocTemplateDefinition docTemplatePrivate in listPrivateTemplateRoots)
			{
				docProject.Templates.Remove(docTemplatePrivate);
			}
		}

		private static void ImportMvdView(ModelView mvdView, DocModelView docView, DocProject docProject, string filepath)
		{
			ImportMvdObject(mvdView, docView);

			docView.BaseView = mvdView.BaseView;
			docView.Exchanges.Clear();
			Dictionary<Guid, ExchangeRequirement> mapExchange = new Dictionary<Guid, ExchangeRequirement>();
			foreach (ExchangeRequirement mvdExchange in mvdView.ExchangeRequirements)
			{
				mapExchange.Add(mvdExchange.Uuid, mvdExchange);

				DocExchangeDefinition docExchange = new DocExchangeDefinition();
				ImportMvdObject(mvdExchange, docExchange);
				docView.Exchanges.Add(docExchange);

				docExchange.Applicability = (DocExchangeApplicabilityEnum)mvdExchange.Applicability;

				// attempt to find icons if exists -- remove extention
				try
				{
					string iconpath = filepath.Substring(0, filepath.Length - 7) + @"\mvd-" + docExchange.Name.ToLower().Replace(' ', '-') + ".png";
					if (System.IO.File.Exists(iconpath))
					{
						docExchange.Icon = System.IO.File.ReadAllBytes(iconpath);
					}
				}
				catch
				{

				}
			}

			foreach (ConceptRoot mvdRoot in mvdView.Roots)
			{
				// find the entity
				DocEntity docEntity = LookupEntity(docProject, mvdRoot.ApplicableRootEntity);
				if (docEntity != null)
				{
					DocConceptRoot docConceptRoot = docView.GetConceptRoot(mvdRoot.Uuid);
					if (docConceptRoot == null)
					{
						docConceptRoot = new DocConceptRoot();
						docView.ConceptRoots.Add(docConceptRoot);
					}

					ImportMvdObject(mvdRoot, docConceptRoot);
					docConceptRoot.ApplicableEntity = docEntity;

					if (mvdRoot.Applicability != null)
					{
						docConceptRoot.ApplicableTemplate = docProject.GetTemplate(mvdRoot.Applicability.Template.Ref);
						if (mvdRoot.Applicability.TemplateRules != null)
						{
							docConceptRoot.ApplicableOperator = (DocTemplateOperator)Enum.Parse(typeof(TemplateOperator), mvdRoot.Applicability.TemplateRules.Operator.ToString());
							foreach (TemplateRule r in mvdRoot.Applicability.TemplateRules.OfType<TemplateRule>())
							{
								DocTemplateItem docItem = ImportMvdItem(r, docProject, mapExchange);
								docConceptRoot.ApplicableItems.Add(docItem);
							}
						}
					}

					docConceptRoot.Concepts.Clear();
					if (mvdRoot.Concepts != null)
					{
						foreach (Concept mvdNode in mvdRoot.Concepts)
						{
							DocTemplateUsage docUse = new DocTemplateUsage();
							docConceptRoot.Concepts.Add(docUse);
							ImportMvdConcept(mvdNode, docUse, docProject, mapExchange);
						}
					}
				}
				else
				{
					//TODO: log error
				}
			}

			// subviews
			if (mvdView.Views != null)
			{
				foreach (ModelView mvdSubView in mvdView.Views)
				{
					DocModelView docSubView = new DocModelView();
					docView.ModelViews.Add(docView);
					ImportMvdView(mvdSubView, docSubView, docProject, filepath);
				}
			}
		}

		private static void ImportMvdRequirement(ConceptRequirement mvdReq, DocExchangeItem docReq, DocProject docProject)
		{
			// TODO: support inner views!!!
			foreach (DocModelView docModel in docProject.ModelViews)
			{
				foreach (DocExchangeDefinition docAnno in docModel.Exchanges)
				{
					if (docAnno.Uuid.Equals(mvdReq.ExchangeRequirement))
					{
						docReq.Exchange = docAnno;
						break;
					}
				}
			}

			docReq.Applicability = (DocExchangeApplicabilityEnum)mvdReq.Applicability;

			switch (mvdReq.Requirement)
			{
				case RequirementEnum.Mandatory:
					docReq.Requirement = DocExchangeRequirementEnum.Mandatory;
					break;

				case RequirementEnum.Recommended:
					docReq.Requirement = DocExchangeRequirementEnum.Optional;
					break;

				case RequirementEnum.NotRelevant:
					docReq.Requirement = DocExchangeRequirementEnum.NotRelevant;
					break;

				case RequirementEnum.NotRecommended:
					docReq.Requirement = DocExchangeRequirementEnum.NotRecommended;
					break;

				case RequirementEnum.Excluded:
					docReq.Requirement = DocExchangeRequirementEnum.Excluded;
					break;
			}
		}

		private static void ImportMvdConcept(Concept mvdNode, DocTemplateUsage docUse, DocProject docProject, Dictionary<Guid, ExchangeRequirement> mapExchange)
		{
			ImportMvdObject(mvdNode, docUse);

			if (mvdNode.Template != null)
			{
				DocTemplateDefinition docTemplateDef = docProject.GetTemplate(mvdNode.Template.Ref);
				if (docTemplateDef != null)
				{
					docUse.Definition = docTemplateDef;
				}
			}

			docUse.Override = mvdNode.Override;

			// exchange requirements
			foreach (ConceptRequirement mvdReq in mvdNode.Requirements)
			{
				ExchangeRequirement mvdExchange = null;
				if (mapExchange.TryGetValue(mvdReq.ExchangeRequirement, out mvdExchange))
				{
					DocExchangeItem docReq = new DocExchangeItem();
					docUse.Exchanges.Add(docReq);
					ImportMvdRequirement(mvdReq, docReq, docProject);
				}
			}

			// rules as template items
			if (mvdNode.TemplateRules != null)
			{
				docUse.Operator = (DocTemplateOperator)Enum.Parse(typeof(DocTemplateOperator), mvdNode.TemplateRules.Operator.ToString());
				foreach (TemplateRule rule in mvdNode.TemplateRules.OfType<TemplateRule>())
				{
					DocTemplateItem docItem = ImportMvdItem(rule, docProject, mapExchange);
					docUse.Items.Add(docItem);
				}
			}
		}


		private static DocTemplateItem ImportMvdItem(TemplateRule ruleItem, DocProject docProject, Dictionary<Guid, ExchangeRequirement> mapExchange)
		{
			DocTemplateItem docItem = new DocTemplateItem();
			docItem.Documentation = ruleItem.Description;
			docItem.RuleInstanceID = ruleItem.RuleID;
			docItem.ParseParameterExpressions(ruleItem.Parameters); // convert from mvdXML

			// V11.6
			if (ruleItem is TemplateItem)
			{
				TemplateItem rule = (TemplateItem)ruleItem;
				//if (rule.Requirements != null)
				//{
				//    foreach (ConceptRequirement mvdReq in rule.Requirements)
				//    {
				//        ExchangeRequirement mvdExchange = null;
				//        if (mapExchange.TryGetValue(mvdReq.ExchangeRequirement, out mvdExchange))
				//        {
				//            DocExchangeItem docReq = new DocExchangeItem();
				//            docItem.Exchanges.Add(docReq);
				//            ImportMvdRequirement(mvdReq, docReq, docProject);
				//        }
				//    }
				//}

				if (rule.References != null)
				{
					foreach (Concept con in rule.References)
					{
						DocTemplateUsage docInner = new DocTemplateUsage();
						docItem.Concepts.Add(docInner);
						ImportMvdConcept(con, docInner, docProject, mapExchange);
					}
				}

				//docItem.Order = rule.Order;
				//switch (rule.Usage)
				//{
				//    case TemplateRuleUsage.System:
				//        docItem.Calculated = true;
				//        break;

				//    case TemplateRuleUsage.Calculation:
				//        docItem.Calculated = true;
				//        docItem.Optional = true;
				//        break;

				//    case TemplateRuleUsage.Reference:
				//        docItem.Reference = true;
				//        break;

				//    case TemplateRuleUsage.Key:
				//        docItem.Key = true;
				//        break;

				//    case TemplateRuleUsage.Optional:
				//        docItem.Optional = true;
				//        break;

				//    case TemplateRuleUsage.Required:
				//        // default
				//        break;
				//}
			}

			return docItem;
		}

		private static void ImportMvdTemplate(ConceptTemplate mvdTemplate, DocTemplateDefinition docDef, Dictionary<EntityRule, DocModelRuleEntity> fixups)
		{
			ImportMvdObject(mvdTemplate, docDef);
			docDef.Type = mvdTemplate.ApplicableEntity;

			docDef.Rules.Clear();
			if (mvdTemplate.Rules != null)
			{
				foreach (AttributeRule mvdRule in mvdTemplate.Rules)
				{
					DocModelRule docRule = ImportMvdRule(mvdRule, fixups);
					docDef.Rules.Add(docRule);
					docRule.ParentRule = null;
				}
			}

			// recurse through subtemplates
			if (mvdTemplate.SubTemplates != null)
			{
				foreach (ConceptTemplate mvdSub in mvdTemplate.SubTemplates)
				{
					DocTemplateDefinition docSub = docDef.GetTemplate(mvdSub.Uuid);
					if (docSub == null)
					{
						docSub = new DocTemplateDefinition();
						docDef.Templates.Add(docSub);
					}
					ImportMvdTemplate(mvdSub, docSub, fixups);
				}
			}
		}

				internal static DocModelRule ImportMvdRule(AttributeRule mvdRule, Dictionary<EntityRule, DocModelRuleEntity> fixups)
		{
			DocModelRuleAttribute docRule = new DocModelRuleAttribute();
			docRule.Name = mvdRule.AttributeName;
			docRule.Description = mvdRule.Description;
			docRule.Identification = mvdRule.RuleID;
			//ImportMvdCardinality(docRule, mvdRule.Cardinality);

			if (mvdRule.EntityRules != null)
			{
				foreach (EntityRule mvdEntityRule in mvdRule.EntityRules)
				{
					DocModelRuleEntity docRuleEntity = new DocModelRuleEntity();
					docRuleEntity.Name = mvdEntityRule.EntityName;
					docRuleEntity.Description = mvdEntityRule.Description;
					docRuleEntity.Identification = mvdEntityRule.RuleID;
					//ImportMvdCardinality(docRule, mvdRule.Cardinality);
					docRule.Rules.Add(docRuleEntity);
					docRuleEntity.ParentRule = docRule;
					if (mvdEntityRule.References != null)
					{
						docRuleEntity.Prefix = mvdEntityRule.References.IdPrefix;
					}

					if (mvdEntityRule.AttributeRules != null)
					{
						foreach (AttributeRule mvdAttributeRule in mvdEntityRule.AttributeRules)
						{
							DocModelRule docRuleAttribute = ImportMvdRule(mvdAttributeRule, fixups);
							docRuleEntity.Rules.Add(docRuleAttribute);
							docRuleAttribute.ParentRule = docRuleEntity;
						}
					}

					if (mvdEntityRule.Constraints != null)
					{
						foreach (Constraint mvdConstraint in mvdEntityRule.Constraints)
						{
							DocModelRuleConstraint docRuleConstraint = new DocModelRuleConstraint();
							docRuleConstraint.Description = mvdConstraint.Expression;
							docRuleEntity.Rules.Add(docRuleConstraint);
							docRuleConstraint.ParentRule = docRuleEntity;
						}
					}

					if (mvdEntityRule.References != null)
					{
						// add it later, as referenced templates may not necessarily be loaded yet
						fixups.Add(mvdEntityRule, docRuleEntity);
					}
				}
			}

			if (mvdRule.Constraints != null)
			{
				foreach (Constraint mvdConstraint in mvdRule.Constraints)
				{
					DocModelRuleConstraint docRuleConstraint = new DocModelRuleConstraint();
					docRuleConstraint.Description = mvdConstraint.Expression;
					docRule.Rules.Add(docRuleConstraint);
					docRuleConstraint.ParentRule = docRule;
				}
			}

			return docRule;
		}


		/// <summary>
		/// Helper function to populate attributes
		/// </summary>
		/// <param name="mvd"></param>
		/// <param name="doc"></param>
		private static void ImportMvdObject(Element mvd, DocObject doc)
		{
			doc.Name = mvd.Name;
			doc.Uuid = mvd.Uuid;
			doc.Version = mvd.Version;
			doc.Owner = mvd.Owner;
			doc.Status = mvd.Status.ToString();
			doc.Copyright = mvd.Copyright;
			doc.Code = mvd.Code;
			doc.Author = mvd.Author;

			if (mvd.Definitions != null)
			{
				foreach (Definition def in mvd.Definitions)
				{
					if (def != null && def.Body != null)
					{
						if (!String.IsNullOrEmpty(def.Body.Lang))
						{
							// mvdXML1.1 now uses this
							DocLocalization loc = new DocLocalization();
							doc.Localization.Add(loc);
							loc.Name = def.Tags;
							loc.Locale = def.Body.Lang;
							loc.Documentation = def.Body.Content;
							loc.Category = DocCategoryEnum.Definition;
						}
						else if (def.Body != null)
						{
							// base definition
							doc.Documentation = def.Body.Content;
						}

						if (def.Links != null)
						{
							// older exports use this
							foreach (Link link in def.Links)
							{
								DocLocalization loc = doc.GetLocalization(link.Lang);
								if (loc == null)
								{
									loc = new DocLocalization();
									doc.Localization.Add(loc);

									try
									{
										loc.Category = (DocCategoryEnum)(CategoryEnum)Enum.Parse(typeof(CategoryEnum), link.Category.ToString());
									}
									catch
									{

									}
									loc.Locale = link.Lang;
									loc.URL = link.Href;
								}

								loc.Name = link.Title;
								if (loc.Documentation == null)
								{
									loc.Documentation = link.Content;
								}
							}
						}
					}
				}
			}
		}

		private static DocEntity LookupEntity(DocProject project, string name)
		{
			// inefficient, but keeps code organized

			foreach (DocSection section in project.Sections)
			{
				foreach (DocSchema schema in section.Schemas)
				{
					foreach (DocEntity entity in schema.Entities)
					{
						if (entity.Name.Equals(name))
						{
							return entity;
						}
					}
				}
			}

			return null;
		}
	}
}
