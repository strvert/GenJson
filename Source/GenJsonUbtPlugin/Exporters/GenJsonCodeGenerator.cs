using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CaseConverter;
using EpicGames.UHT.Tables;
using EpicGames.UHT.Types;
using EpicGames.UHT.Utils;
using Microsoft.Extensions.Logging;

namespace GenJsonUbtPlugin.Exporters;

[UnrealHeaderTool]
public class GenJsonCodeGenerator
{
	private enum FieldNameCase
	{
		CamelCase,
		KebabCase,
		PascalCase,
		SnakeCase,
	}

	private static string ConvertFieldName(string name, FieldNameCase? renameAll)
	{
		return renameAll switch
		{
			FieldNameCase.CamelCase => name.ToCamelCase(),
			FieldNameCase.KebabCase => name.ToKebabCase(),
			FieldNameCase.PascalCase => name.ToPascalCase(),
			FieldNameCase.SnakeCase => name.ToSnakeCase(),
			_ => name
		};
	}

	private struct StructRecord(UhtStruct type, FieldNameCase? field)
	{
		public readonly FieldNameCase? RenameAll = field;
		public readonly UhtStruct Type = type;
	}

	private struct EnumRecord(FieldNameCase? field, UhtEnum type)
	{
		public readonly FieldNameCase? RenameAll = field;
		public readonly UhtEnum Type = type;
	}

	private class TypesInHeader
	{
		private readonly List<StructRecord> _serializableStructs = new();
		private readonly List<StructRecord> _deserializableStructs = new();
		private readonly List<EnumRecord> _serializableEnums = new();
		private readonly List<EnumRecord> _deserializableEnums = new();

		public void AddSerializableStruct(StructRecord record) => _serializableStructs.Add(record);
		public void AddDeserializableStruct(StructRecord record) => _deserializableStructs.Add(record);
		public void AddSerializableEnum(EnumRecord record) => _serializableEnums.Add(record);
		public void AddDeserializableEnum(EnumRecord record) => _deserializableEnums.Add(record);

		public IReadOnlyList<StructRecord> SerializableStructs => _serializableStructs;
		public IReadOnlyList<StructRecord> DeserializableStructs => _deserializableStructs;
		public IReadOnlyList<EnumRecord> SerializableEnums => _serializableEnums;
		public IReadOnlyList<EnumRecord> DeserializableEnums => _deserializableEnums;
	}

	private static void LoadDependencies(UhtSession session)
	{
		var currentAssembly = Assembly.GetExecutingAssembly();
		var directory = Path.GetDirectoryName(currentAssembly.Location)!;
		Assembly.LoadFrom(Path.Combine(directory, "CaseConverter.dll"));
	}

	[UhtExporter(Name = "GenJsonCodeGen", Description = "Json serialize / deserialize code generator",
		Options = UhtExporterOptions.Default, HeaderFilters = ["*.genjson.h"], ModuleName = "GenJson")]
	public static void CodeGenerator(IUhtExportFactory factory)
	{
		UhtSession session = factory.Session;

		LoadDependencies(session);

		session.Logger.LogInformation("GenJson code generation started");

		Dictionary<UhtHeaderFile, TypesInHeader> files = new();

		foreach (var module in session.Modules)
		{
			if (module.IsPartOfEngine)
			{
				continue;
			}

			session.Logger.LogInformation("Processing module {0}", module.ShortName);

			CollectStructExports(session, module.ScriptPackage, files);
			CollectEnumExports(session, module.ScriptPackage, files);
		}

		foreach (var (header, structs) in files)
		{
			AppendFile(factory, header, structs);
		}
	}

	private static void AppendFile(IUhtExportFactory factory, UhtHeaderFile header, TypesInHeader types)
	{
		using var borrower = new BorrowStringBuilder(StringBuilderCache.Big);
		var builder = borrower.StringBuilder;

		AppendHeaders(builder);

		builder.Append("\r\n");

		AppendDefineStart(builder, "GENJSON_GENERATED_SERIALIZERS");

		foreach (var serializable in types.SerializableStructs)
		{
			AppendSerializeStructSpecialization(builder, serializable);
			builder.Append("\\\r\n");
		}

		foreach (var serializable in types.SerializableEnums)
		{
			AppendSerializeEnumSpecialization(builder, serializable);
			builder.Append("\\\r\n");
		}

		// foreach (var deserializable in structs.Deserializable)
		// {
		// 	AppendTemplateSpecialization(builder, deserializable.Type, deserializable.Type.ParamProperties);
		// 	builder.Append("\r\n");
		// }

		var outputFilePath = factory.MakePath(header.FileNameWithoutExtension, ".genjson.h");
		factory.CommitOutput(outputFilePath, borrower.StringBuilder);
	}

	private static void AppendDefineStart(StringBuilder builder, string name)
	{
		builder.Append($"#undef {name}\r\n");
		builder.Append($"#define {name}(...) \\\r\n");
	}

	private static void AppendHeaders(StringBuilder builder)
	{
		builder.Append("#include \"GenJsonSerializer.h\"\r\n");
		builder.Append("#include <type_traits>\r\n");
	}

	private static void AppendSerializeEnumSpecialization(StringBuilder builder, EnumRecord enumType)
	{
		builder.Append("template <> \\\r\n");
		builder.Append($"struct GenJson::TSerializer<::{enumType.Type.SourceName}> \\\r\n");
		builder.Append("{ \\\r\n");
		{
			AppendWriteEnumFunction(builder, enumType);
		}
		builder.Append("}; \\\r\n");
	}

	private static void AppendSerializeStructSpecialization(StringBuilder builder, StructRecord record)
	{
		builder.Append("template <> \\\r\n");
		builder.Append($"struct GenJson::TSerializer<::{record.Type.SourceName}> \\\r\n");
		builder.Append("{ \\\r\n");
		{
			AppendWriteStructFunction(builder, record);
		}
		builder.Append("}; \\\r\n");
	}

	private static void AppendWriteEnumFunction(StringBuilder builder, EnumRecord record)
	{
		builder.Append($"\tstatic bool Write(const {record.Type.SourceName}& Instance, FJsonWriter& Writer) \\\r\n");
		builder.Append("{ \\\r\n");
		{
			if (record.Type.MetaData.TryGetValue("AsNumber", out var asNumber))
			{
				AppendWriteEnumAsNumber(builder, record);
			}
			else
			{
				AppendWriteEnumAsString(builder, record);
			}
		}
		builder.Append("} \\\r\n");
	}

	private static void AppendWriteEnumAsNumber(StringBuilder builder, EnumRecord record)
	{
		builder.Append(
			$"return GenJson::Write(static_cast<std::underlying_type_t<{record.Type.SourceName}>>(Instance), Writer); \\\r\n");
	}

	private static void AppendWriteEnumAsString(StringBuilder builder, EnumRecord record)
	{
		builder.Append("switch (Instance) \\\r\n");
		builder.Append("{ \\\r\n");

		foreach (var value in record.Type.EnumValues)
		{
			AppendWriteEnumCase(builder, record, value);
		}

		builder.Append("} \\\r\n");
		builder.Append("return false; \\\r\n");
	}

	private static string GetEnumValueName(UhtEnum type, UhtEnumValue value, FieldNameCase? field)
	{
		if (type.MetaData.TryGetValue("Rename", (int)value.Value, out var rename))
		{
			return rename;
		}

		var name = value.Name[(value.Name.LastIndexOf("::", StringComparison.Ordinal) + 2)..];
		return field.HasValue ? ConvertFieldName(name, field) : name;
	}

	private static void AppendWriteEnumCase(StringBuilder builder, EnumRecord record, UhtEnumValue value)
	{
		var name = GetEnumValueName(record.Type, value, record.RenameAll);
		builder.Append($"case {value.Name}: \\\r\n");
		builder.Append("{ \\\r\n");
		{
			builder.Append($"return GenJson::Write(TEXT(\"{name}\"), Writer); \\\r\n");
		}
		builder.Append("} \\\r\n");
	}

	private static void AppendWriteStructFunction(StringBuilder builder, StructRecord record)
	{
		builder.Append($"\tstatic bool Write(const {record.Type.SourceName}& Instance, FJsonWriter& Writer) \\\r\n");
		builder.Append("{ \\\r\n");
		{
			AppendStartObject(builder);
			foreach (var property in record.Type.Properties)
			{
				AppendWriteProperty(builder, property, record.RenameAll);
			}

			AppendEndObject(builder);
			builder.Append("return true; \\\r\n");
		}
		builder.Append("} \\\r\n");
	}

	private static void AppendStartObject(StringBuilder builder)
	{
		builder.Append("Writer.StartObject(); \\\r\n");
	}

	private static void AppendEndObject(StringBuilder builder)
	{
		builder.Append("Writer.EndObject(); \\\r\n");
	}

	private static string GetPropertyName(UhtProperty property, FieldNameCase? renameAll)
	{
		if (property.MetaData.TryGetValue("Rename", out var rename))
		{
			return rename;
		}

		var name = property.SourceName;
		return renameAll.HasValue ? ConvertFieldName(name, renameAll) : name;
	}

	private static void AppendWriteProperty(StringBuilder builder, UhtProperty property, FieldNameCase? renameAll)
	{
		builder.Append($"Writer.Key(TEXT(\"{GetPropertyName(property, renameAll)}\")); \\\r\n");
		builder.Append($"GenJson::Write(Instance.{property.SourceName}, Writer); \\\r\n");
	}

	private static void CollectExports<T>(UhtSession session, UhtType type,
		Dictionary<UhtHeaderFile, TypesInHeader> exportMap,
		Action<TypesInHeader, T> addSerializable,
		Action<TypesInHeader, T> addDeserializable) where T : UhtType
	{
		if (type is not T targetType)
		{
			foreach (var child in type.Children)
			{
				CollectExports(session, child, exportMap, addSerializable, addDeserializable);
			}

			return;
		}

		var isSerializable = type.MetaData.ContainsKey("Serialize");
		var isDeserializable = type.MetaData.ContainsKey("Deserialize");

		if (!isSerializable && !isDeserializable)
		{
			return;
		}

		if (!exportMap.TryGetValue(targetType.HeaderFile, out var export))
		{
			export = new TypesInHeader();
			exportMap[targetType.HeaderFile] = export;
		}

		if (isSerializable)
		{
			addSerializable(export, targetType);
		}

		if (isDeserializable)
		{
			addDeserializable(export, targetType);
		}
	}

	private static void CollectEnumExports(UhtSession session, UhtType type,
		Dictionary<UhtHeaderFile, TypesInHeader> serializableEnums)
	{
		CollectExports(session, type, serializableEnums,
			CreateEnumHandler((export, enumType) => export.AddSerializableEnum(enumType)),
			CreateEnumHandler((export, enumType) => export.AddDeserializableEnum(enumType)));
	}

	private static Action<TypesInHeader, UhtEnum> CreateEnumHandler(Action<TypesInHeader, EnumRecord> addAction)
	{
		return (export, enumType) =>
		{
			var field = GetRenameAll(enumType);
			addAction(export, new EnumRecord(field, enumType));
		};
	}

	private static void CollectStructExports(UhtSession session, UhtType type,
		Dictionary<UhtHeaderFile, TypesInHeader> serializableStructs)
	{
		CollectExports<UhtStruct>(session, type, serializableStructs,
			CreateStructHandler((export, structType) => export.AddSerializableStruct(structType)),
			CreateStructHandler((export, structType) => export.AddDeserializableStruct(structType)));
	}

	private static Action<TypesInHeader, UhtStruct> CreateStructHandler(Action<TypesInHeader, StructRecord> addAction)
	{
		return (export, structType) =>
		{
			var field = GetRenameAll(structType);
			addAction(export, new StructRecord(structType, field));
		};
	}

	private static FieldNameCase? GetRenameAll(UhtField enumType)
	{
		if (!enumType.MetaData.TryGetValue("RenameAll", out var value))
		{
			return null;
		}

		return value switch
		{
			"camelCase" => FieldNameCase.CamelCase,
			"kebab-case" => FieldNameCase.KebabCase,
			"PascalCase" => FieldNameCase.PascalCase,
			"snake_case" => FieldNameCase.SnakeCase,
			_ => null
		};
	}
}