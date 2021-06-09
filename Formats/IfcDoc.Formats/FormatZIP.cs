// Name:        FormatZIP.cs
// Description: Generates zip files containing property sets and quantity sets
// Author:      Tim Chipman
// Origination: Work performed for BuildingSmart by Constructivity.com LLC.
// Copyright:   (c) 2016 BuildingSmart International Ltd.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Packaging;

using IfcDoc.Format.XML;
using IfcDoc.Schema.DOC;
using IfcDoc.Schema.PSD;

namespace IfcDoc
{
	/// <summary>
	/// Zip file for storing property sets and quantity sets
	/// </summary>
	public class FormatZIP : IDisposable
	{
		Stream m_stream;
		DocProject m_project;
		Dictionary<DocObject, bool> m_included;
		Type m_type;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="stream">Stream for the zip file.</param>
		/// <param name="project">Project.</param>
		/// <param name="included">Map of included definitions</param>
		/// <param name="type">Optional type of data to export, or null for all; DocPropertySet, DocQuantitySet are valid</param>
		public FormatZIP(Stream stream, DocProject project, Dictionary<DocObject, bool> included, Type type)
		{
			this.m_stream = stream;
			this.m_project = project;
			this.m_included = included;
			this.m_type = type;
		}
		public void Save()
		{
			// build map of enumerations
			Dictionary<string, DocPropertyEnumeration> mapPropEnum = new Dictionary<string, DocPropertyEnumeration>();
			foreach (DocPropertyEnumeration docEnum in this.m_project.PropertyEnumerations)
			{
				if (!mapPropEnum.ContainsKey(docEnum.Name))
				{
					mapPropEnum.Add(docEnum.Name, docEnum);
				}
			}

			using (Package zip = ZipPackage.Open(this.m_stream, FileMode.Create))
			{
				foreach (DocSection docSection in this.m_project.Sections)
				{
					foreach (DocSchema docSchema in docSection.Schemas)
					{
						if (this.m_type == null || this.m_type == typeof(DocPropertySet))
						{
							foreach (DocPropertySet docPset in docSchema.PropertySets)
							{
								if (m_included == null || this.m_included.ContainsKey(docPset))
								{
									if (docPset.IsVisible())
									{
										Uri uri = PackUriHelper.CreatePartUri(new Uri(docPset.Name + ".xml", UriKind.Relative));
										PackagePart part = zip.CreatePart(uri, "", CompressionOption.Normal);
										using (Stream refstream = part.GetStream())
										{
											refstream.SetLength(0);
											PropertySetDef psd = ExportPsd(docPset, mapPropEnum, this.m_project);
											using (FormatXML format = new FormatXML(refstream, typeof(PropertySetDef), PropertySetDef.DefaultNamespace, null))
											{
												format.Instance = psd;
												format.Save();
											}
										}
									}
								}
							}
						}

						if (this.m_type == null || this.m_type == typeof(DocQuantitySet))
						{
							foreach (DocQuantitySet docQset in docSchema.QuantitySets)
							{
								if (m_included == null || this.m_included.ContainsKey(docQset))
								{
									Uri uri = PackUriHelper.CreatePartUri(new Uri(docQset.Name + ".xml", UriKind.Relative));
									PackagePart part = zip.CreatePart(uri, "", CompressionOption.Normal);
									using (Stream refstream = part.GetStream())
									{
										refstream.SetLength(0);
										QtoSetDef psd = ExportQto(docQset, this.m_project);
										using (FormatXML format = new FormatXML(refstream, typeof(QtoSetDef), PropertySetDef.DefaultNamespace, null))
										{
											format.Instance = psd;
											format.Save();
										}
									}
								}
							}
						}
					}
				}
			}

		}

		public void Dispose()
		{
			this.m_stream.Close();
		}

		public static PropertySetDef ExportPsd(DocPropertySet docPset, Dictionary<string, DocPropertyEnumeration> mapEnum, DocProject docProject)
		{
			DocEntity[] appents = docPset.GetApplicableTypeDefinitions(docProject);
			DocEntity applicableentity = null;
			if (appents != null && appents.Length > 0 && appents[0] != null)
			{
				applicableentity = docProject.GetDefinition(appents[0].BaseDefinition) as DocEntity;
			}

			string[] apptypes = new string[0];
			if (docPset.ApplicableType != null)
			{
				apptypes = docPset.ApplicableType.Split(',');
			}

			// convert to psd schema
			PropertySetDef psd = new PropertySetDef();
			psd.Versions = new List<IfcVersion>();
			psd.Versions.Add(new IfcVersion());
			psd.Versions[0].version = docProject.GetSchemaIdentifier();
			psd.Name = docPset.Name;
			psd.Definition = docPset.Documentation;
			psd.TemplateType = docPset.PropertySetType;
			psd.ApplicableTypeValue = docPset.ApplicableType;
			psd.ApplicableClasses = new List<ClassName>();
			foreach (string app in apptypes)
			{
				ClassName cln = new ClassName();
				cln.Value = app;
				psd.ApplicableClasses.Add(cln);
			}
			psd.IfdGuid = docPset.Uuid.ToString("N");

			psd.PsetDefinitionAliases = new List<PsetDefinitionAlias>();
			foreach (DocLocalization docLocal in docPset.Localization)
			{
				PsetDefinitionAlias alias = new PsetDefinitionAlias();
				psd.PsetDefinitionAliases.Add(alias);
				alias.lang = docLocal.Locale;
				alias.Value = docLocal.Documentation;
			}

			psd.PropertyDefs = new List<PropertyDef>();
			foreach (DocProperty docProp in docPset.Properties)
			{
				PropertyDef prop = new PropertyDef();
				psd.PropertyDefs.Add(prop);

				// check for inherited property
				DocProperty docSuper = docProp;
				if (applicableentity != null)
				{
					docSuper = docProject.FindProperty(prop.Name, applicableentity);
					if (docSuper == null)
					{
						docSuper = docProp;
					}
				}
				ExportPsdProperty(docSuper, prop, mapEnum);
			}

			return psd;
		}

		private static void ExportPsdProperty(DocProperty docProp, PropertyDef prop, Dictionary<string, DocPropertyEnumeration> mapPropEnum)
		{
			prop.IfdGuid = docProp.Uuid.ToString("N");
			prop.Name = docProp.Name;
			prop.Definition = docProp.Documentation;

			prop.NameAliases = new List<NameAlias>();
			prop.DefinitionAliases = new List<DefinitionAlias>();
			foreach (DocLocalization docLocal in docProp.Localization)
			{
				NameAlias na = new NameAlias();
				prop.NameAliases.Add(na);
				na.lang = docLocal.Locale;
				na.Value = docLocal.Name;

				DefinitionAlias da = new DefinitionAlias();
				prop.DefinitionAliases.Add(da);
				da.lang = docLocal.Locale;
				da.Value = docLocal.Documentation;
			}

			prop.PropertyType = new PropertyType();
			switch (docProp.PropertyType)
			{
				case DocPropertyTemplateTypeEnum.P_SINGLEVALUE:
					prop.PropertyType.TypePropertySingleValue = new TypePropertySingleValue();
					prop.PropertyType.TypePropertySingleValue.DataType = new DataType();
					prop.PropertyType.TypePropertySingleValue.DataType.type = docProp.PrimaryDataType;
					break;

				case DocPropertyTemplateTypeEnum.P_BOUNDEDVALUE:
					prop.PropertyType.TypePropertyBoundedValue = new TypePropertyBoundedValue();
					prop.PropertyType.TypePropertyBoundedValue.DataType = new DataType();
					prop.PropertyType.TypePropertyBoundedValue.DataType.type = docProp.PrimaryDataType;
					break;

				case DocPropertyTemplateTypeEnum.P_ENUMERATEDVALUE:
					prop.PropertyType.TypePropertyEnumeratedValue = new TypePropertyEnumeratedValue();
					prop.PropertyType.TypePropertyEnumeratedValue.EnumList = new EnumList();
					{
						if (docProp.Enumeration != null)
						{
							DocPropertyEnumeration docPropEnum = docProp.Enumeration;
							prop.PropertyType.TypePropertyEnumeratedValue.EnumList.name = docPropEnum.Name;
							prop.PropertyType.TypePropertyEnumeratedValue.EnumList.Items = new List<EnumItem>();
							prop.PropertyType.TypePropertyEnumeratedValue.ConstantList = new ConstantList();
							prop.PropertyType.TypePropertyEnumeratedValue.ConstantList.Items = new List<ConstantDef>();

							foreach (DocPropertyConstant constant in docPropEnum.Constants)
							{
								EnumItem eni = new EnumItem();
								prop.PropertyType.TypePropertyEnumeratedValue.EnumList.Items.Add(eni);
								eni.Value = constant.Name;
								ConstantDef con = new ConstantDef();

								con.Name = constant.Name.Trim();
								con.Definition = constant.Documentation;
								con.NameAliases = new List<NameAlias>();
								con.DefinitionAliases = new List<DefinitionAlias>();

								prop.PropertyType.TypePropertyEnumeratedValue.ConstantList.Items.Add(con);

								foreach (DocLocalization docLocal in constant.Localization)
								{
									NameAlias na = new NameAlias();
									con.NameAliases.Add(na);
									na.lang = docLocal.Locale;
									if (!string.IsNullOrEmpty(docLocal.Name))
									{
										na.Value = docLocal.Name.Trim();
									}
									else
									{
										na.Value = "";
									}

									if (!string.IsNullOrEmpty(docLocal.Documentation))
									{
										DefinitionAlias da = new DefinitionAlias();
										con.DefinitionAliases.Add(da);
										da.lang = docLocal.Locale;
										da.Value = docLocal.Documentation;
									}
								}
							}
						}
					}

					break;

				case DocPropertyTemplateTypeEnum.P_LISTVALUE:
					prop.PropertyType.TypePropertyListValue = new TypePropertyListValue();
					prop.PropertyType.TypePropertyListValue.ListValue = new ListValue();
					prop.PropertyType.TypePropertyListValue.ListValue.DataType = new DataType();
					prop.PropertyType.TypePropertyListValue.ListValue.DataType.type = docProp.PrimaryDataType;
					break;

				case DocPropertyTemplateTypeEnum.P_TABLEVALUE:
					prop.PropertyType.TypePropertyTableValue = new TypePropertyTableValue();
					prop.PropertyType.TypePropertyTableValue.Expression = String.Empty;
					prop.PropertyType.TypePropertyTableValue.DefiningValue = new DefiningValue();
					prop.PropertyType.TypePropertyTableValue.DefiningValue.DataType = new DataType();
					prop.PropertyType.TypePropertyTableValue.DefiningValue.DataType.type = docProp.PrimaryDataType;
					prop.PropertyType.TypePropertyTableValue.DefinedValue = new DefinedValue();
					prop.PropertyType.TypePropertyTableValue.DefinedValue.DataType = new DataType();
					prop.PropertyType.TypePropertyTableValue.DefinedValue.DataType.type = docProp.SecondaryDataType;
					break;

				case DocPropertyTemplateTypeEnum.P_REFERENCEVALUE:
					prop.PropertyType.TypePropertyReferenceValue = new TypePropertyReferenceValue();
					prop.PropertyType.TypePropertyReferenceValue.reftype = docProp.PrimaryDataType;
					break;

				case DocPropertyTemplateTypeEnum.COMPLEX:
					prop.PropertyType.TypeComplexProperty = new TypeComplexProperty();
					prop.PropertyType.TypeComplexProperty.name = docProp.PrimaryDataType;
					prop.PropertyType.TypeComplexProperty.PropertyDefs = new List<PropertyDef>();
					foreach (DocProperty docSub in docProp.Elements)
					{
						PropertyDef sub = new PropertyDef();
						prop.PropertyType.TypeComplexProperty.PropertyDefs.Add(sub);
						ExportPsdProperty(docSub, sub, mapPropEnum);
					}
					break;
			}
		}

		public static QtoSetDef ExportQto(DocQuantitySet docPset, DocProject docProject)
		{
			string[] apptypes = new string[0];
			if (docPset.ApplicableType != null)
			{
				apptypes = docPset.ApplicableType.Split(',');
			}

			// convert to psd schema
			QtoSetDef psd = new QtoSetDef();
			psd.Name = docPset.Name;
			psd.Definition = docPset.Documentation;
			psd.Versions = new List<IfcVersion>();
			psd.Versions.Add(new IfcVersion());
			psd.Versions[0].version = docProject.GetSchemaIdentifier();
			psd.ApplicableTypeValue = docPset.ApplicableType;
			psd.ApplicableClasses = new List<ClassName>();
			foreach (string app in apptypes)
			{
				ClassName cln = new ClassName();
				cln.Value = app;
				psd.ApplicableClasses.Add(cln);
			}

			psd.QtoDefinitionAliases = new List<QtoDefinitionAlias>();
			foreach (DocLocalization docLocal in docPset.Localization)
			{
				QtoDefinitionAlias alias = new QtoDefinitionAlias();
				psd.QtoDefinitionAliases.Add(alias);
				alias.lang = docLocal.Locale;
				alias.Value = docLocal.Documentation;
			}

			psd.QtoDefs = new List<QtoDef>();
			foreach (DocQuantity docProp in docPset.Quantities)
			{
				QtoDef prop = new QtoDef();
				psd.QtoDefs.Add(prop);
				prop.Name = docProp.Name;
				prop.Definition = docProp.Documentation;

				prop.NameAliases = new List<NameAlias>();
				prop.DefinitionAliases = new List<DefinitionAlias>();
				foreach (DocLocalization docLocal in docProp.Localization)
				{
					NameAlias na = new NameAlias();
					prop.NameAliases.Add(na);
					na.lang = docLocal.Locale;
					na.Value = docLocal.Name;

					DefinitionAlias da = new DefinitionAlias();
					prop.DefinitionAliases.Add(da);
					da.lang = docLocal.Locale;
					da.Value = docLocal.Documentation;
				}

				prop.QtoType = docProp.QuantityType.ToString();
			}

			return psd;
		}
	}
}
