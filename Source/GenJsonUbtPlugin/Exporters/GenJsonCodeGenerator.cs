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

/// <summary>
/// フィールド名の変換方式
/// </summary>
internal enum FieldNameCase
{
	CamelCase,
	KebabCase,
	PascalCase,
	SnakeCase,
}

/// <summary>
/// UHT にフックされる GenJson のコードジェネレータ。
/// </summary>
[UnrealHeaderTool]
public sealed class GenJsonCodeGenerator
{
	/// <summary>
	/// 構造体に関するメタ情報
	/// </summary>
	private readonly record struct StructRecord(UhtStruct Type, FieldNameCase? RenameAll);

	/// <summary>
	/// 列挙型に関するメタ情報
	/// </summary>
	private readonly record struct EnumRecord(FieldNameCase? RenameAll, UhtEnum Type);

	/// <summary>
	/// ヘッダーファイルごとの構造体/列挙型の格納クラス
	/// </summary>
	private sealed class TypesInHeader
	{
		private readonly List<StructRecord> _serializableStructs = [];
		private readonly List<StructRecord> _deserializableStructs = [];
		private readonly List<EnumRecord> _serializableEnums = [];
		private readonly List<EnumRecord> _deserializableEnums = [];

		public IReadOnlyList<StructRecord> SerializableStructs => _serializableStructs;
		public IReadOnlyList<StructRecord> DeserializableStructs => _deserializableStructs;
		public IReadOnlyList<EnumRecord> SerializableEnums => _serializableEnums;
		public IReadOnlyList<EnumRecord> DeserializableEnums => _deserializableEnums;

		public void AddSerializableStruct(StructRecord record) => _serializableStructs.Add(record);
		public void AddDeserializableStruct(StructRecord record) => _deserializableStructs.Add(record);
		public void AddSerializableEnum(EnumRecord record) => _serializableEnums.Add(record);
		public void AddDeserializableEnum(EnumRecord record) => _deserializableEnums.Add(record);
	}

	/// <summary>
	/// UHT から呼び出されるコードジェネレータのエントリポイント。
	/// </summary>
	[UhtExporter(
		Name = "GenJsonCodeGen",
		Description = "Json serialize / deserialize code generator",
		Options = UhtExporterOptions.Default,
		HeaderFilters = new[] { "*.genjson.h" },
		ModuleName = "GenJson"
	)]
	public static void CodeGenerator(IUhtExportFactory factory)
	{
		UhtSession session = factory.Session;

		LoadDependencies(session);

		session.Logger.LogInformation("GenJson code generation started.");

		var filesByHeader = new Dictionary<UhtHeaderFile, TypesInHeader>();

		// 各ModuleをスキャンしてSerialize/Deserialize対象を収集
		foreach (UhtModule module in session.Modules)
		{
			if (module.IsPartOfEngine)
			{
				continue;
			}

			session.Logger.LogInformation("Processing module {ModuleName}", module.ShortName);

			CollectStructExports(session, module.ScriptPackage, filesByHeader);
			CollectEnumExports(session, module.ScriptPackage, filesByHeader);
		}

		// 収集したデータを基に実際のコードを書き出す
		foreach ((UhtHeaderFile header, TypesInHeader types) in filesByHeader)
		{
			GenerateCodeForHeader(factory, header, types);
		}
	}

	#region Entry/Initialization

	/// <summary>
	/// 依存関係のロード。UHT が UHT プラグインの依存関係をロードしないため、手動でロードする必要がある。
	/// </summary>
	private static void LoadDependencies(UhtSession session)
	{
		var currentAssembly = Assembly.GetExecutingAssembly();
		var directory = Path.GetDirectoryName(currentAssembly.Location)
		                ?? throw new DirectoryNotFoundException("Current assembly directory not found.");

		Assembly.LoadFrom(Path.Combine(directory, "CaseConverter.dll"));

		session.Logger.LogInformation("Dependencies loaded from {Directory}", directory);
	}

	#endregion

	#region Main Code Generation Flow

	/// <summary>
	/// 指定したHeader用にコードを生成して出力ファイルに書き出す。
	/// </summary>
	private static void GenerateCodeForHeader(IUhtExportFactory factory, UhtHeaderFile header, TypesInHeader types)
	{
		using var borrower = new BorrowStringBuilder(StringBuilderCache.Big);
		var builder = borrower.StringBuilder;

		AppendCommonHeaders(builder);

		builder.AppendLine();
		AppendDefineStart(builder, "GENJSON_SERIALIZERS");

		// Serialize対象Structを出力
		foreach (StructRecord record in types.SerializableStructs)
		{
			AppendSerializeStructSpecialization(builder, record);
			builder.AppendLine("\\");
		}

		// Serialize対象Enumを出力
		foreach (EnumRecord record in types.SerializableEnums)
		{
			AppendSerializeEnumSpecialization(builder, record);
			builder.AppendLine("\\");
		}

		var outputFilePath = factory.MakePath(header.FileNameWithoutExtension, ".genjson.h");
		factory.CommitOutput(outputFilePath, borrower.StringBuilder);
	}

	#endregion

	#region Header Generation Helpers

	/// <summary>
	/// 共通のヘッダー
	/// </summary>
	private static void AppendCommonHeaders(StringBuilder builder)
	{
		builder.AppendLine("#include \"GenJsonSerializer.h\"");
		builder.AppendLine("#include <type_traits>");
	}

	/// <summary>
	/// マクロ定義の開始部分
	/// </summary>
	private static void AppendDefineStart(StringBuilder builder, string name)
	{
		builder.AppendLine($"#undef {name}");
		builder.AppendLine($"#define {name}(...) \\");
	}

	#endregion

	#region Serialize Enum

	/// <summary>
	/// EnumのTSerializerテンプレート特殊化を定義
	/// </summary>
	private static void AppendSerializeEnumSpecialization(StringBuilder builder, EnumRecord enumType)
	{
		builder.AppendLine("template <> \\");
		builder.AppendLine($"struct GenJson::TSerializer<::{enumType.Type.SourceName}> \\");
		builder.AppendLine("{ \\");
		AppendWriteEnumFunction(builder, enumType);
		builder.AppendLine("}; \\");
	}

	private static void AppendWriteEnumFunction(StringBuilder builder, EnumRecord record)
	{
		builder.AppendLine($"\tstatic bool Write(const {record.Type.SourceName}& Instance, FJsonWriter& Writer) \\");
		builder.AppendLine("{ \\");
		if (record.Type.MetaData.TryGetValue("AsNumber", out _))
		{
			// 数値として出力
			AppendWriteEnumAsNumber(builder, record);
		}
		else
		{
			// 文字列として出力
			AppendWriteEnumAsString(builder, record);
		}

		builder.AppendLine("} \\");
	}

	private static void AppendWriteEnumAsNumber(StringBuilder builder, EnumRecord record)
	{
		builder.AppendLine(
			$"return GenJson::Write(static_cast<std::underlying_type_t<{record.Type.SourceName}>>(Instance), Writer); \\");
	}

	private static void AppendWriteEnumAsString(StringBuilder builder, EnumRecord record)
	{
		builder.AppendLine("switch (Instance) \\");
		builder.AppendLine("{ \\");

		foreach (UhtEnumValue value in record.Type.EnumValues)
		{
			AppendWriteEnumCase(builder, record, value);
		}

		builder.AppendLine("} \\");
		builder.AppendLine("return false; \\");
	}

	private static void AppendWriteEnumCase(StringBuilder builder, EnumRecord record, UhtEnumValue value)
	{
		string name = GetEnumValueName(record.Type, value, record.RenameAll);
		builder.AppendLine($"case {value.Name}: \\");
		builder.AppendLine("{ \\");
		builder.AppendLine($"    return GenJson::Write(TEXT(\"{name}\"), Writer); \\");
		builder.AppendLine("} \\");
	}

	#endregion

	#region Serialize Struct

	/// <summary>
	/// StructのTSerializerテンプレート特殊化を定義
	/// </summary>
	private static void AppendSerializeStructSpecialization(StringBuilder builder, StructRecord record)
	{
		builder.AppendLine("template <> \\");
		builder.AppendLine($"struct GenJson::TSerializer<::{record.Type.SourceName}> \\");
		builder.AppendLine("{ \\");
		AppendWriteStructFunction(builder, record);
		builder.AppendLine("}; \\");
		
		// BP 対応は未実装のためコメントアウト
		// if (record.Type.MetaData.ContainsKey("BlueprintType"))
		// {
		// 	AppendRegisterStructSerializer(builder, record);
		// }
	}
	
	private static void AppendRegisterStructSerializer(StringBuilder builder, StructRecord record)
	{
		builder.AppendLine($"GENJSON_REGISTER_STRUCT_SERIALIZER({record.Type.SourceName}); \\");
	}

	private static void AppendWriteStructFunction(StringBuilder builder, StructRecord record)
	{
		builder.AppendLine($"\tstatic bool Write(const {record.Type.SourceName}& Instance, FJsonWriter& Writer) \\");
		builder.AppendLine("{ \\");
		builder.AppendLine("    Writer.StartObject(); \\");
		foreach (UhtProperty property in record.Type.Properties)
		{
			AppendWriteProperty(builder, property, record.RenameAll);
		}

		builder.AppendLine("    Writer.EndObject(); \\");
		builder.AppendLine("    return true; \\");
		builder.AppendLine("} \\");
	}

	private static void AppendWriteProperty(StringBuilder builder, UhtProperty property, FieldNameCase? renameAll)
	{
		string propName = GetPropertyName(property, renameAll);
		builder.AppendLine($"    Writer.Key(TEXT(\"{propName}\")); \\");
		builder.AppendLine($"    GenJson::Write(Instance.{property.SourceName}, Writer); \\");
	}

	#endregion

	#region Name Conversions

	private static string GetEnumValueName(UhtEnum type, UhtEnumValue value, FieldNameCase? field)
	{
		// Enumの各Valueごとに指定された "Rename" を優先
		if (type.MetaData.TryGetValue("Rename", (int)value.Value, out var rename))
		{
			return rename;
		}

		// それがなければソース名の末尾部分を使う
		var name = value.Name[(value.Name.LastIndexOf("::", StringComparison.Ordinal) + 2)..];
		return field.HasValue ? ConvertFieldName(name, field.Value) : name;
	}

	private static string GetPropertyName(UhtProperty property, FieldNameCase? renameAll)
	{
		// メタデータで "Rename" 指定があれば最優先
		if (property.MetaData.TryGetValue("Rename", out var rename))
		{
			return rename;
		}

		// GlobalなRenameAllの指定があれば変換
		return renameAll.HasValue
			? ConvertFieldName(property.SourceName, renameAll.Value)
			: property.SourceName;
	}

	private static FieldNameCase? GetRenameAll(UhtField uhtField)
	{
		if (!uhtField.MetaData.TryGetValue("RenameAll", out string? value))
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

	private static string ConvertFieldName(string name, FieldNameCase renameStrategy)
	{
		return renameStrategy switch
		{
			FieldNameCase.CamelCase => name.ToCamelCase(),
			FieldNameCase.KebabCase => name.ToKebabCase(),
			FieldNameCase.PascalCase => name.ToPascalCase(),
			FieldNameCase.SnakeCase => name.ToSnakeCase(),
			_ => name // fallback
		};
	}

	#endregion

	#region Collect Exports (Structs & Enums)

	/// <summary>
	/// UhtType のツリーを再帰的に探索して、Structに対するSerialize/Deserialize対象を収集する。
	/// </summary>
	private static void CollectStructExports(
		UhtSession session,
		UhtType type,
		Dictionary<UhtHeaderFile, TypesInHeader> exportMap
	)
	{
		CollectExports<UhtStruct>(
			session,
			type,
			exportMap,
			CreateStructHandler((export, structType) => export.AddSerializableStruct(structType)),
			CreateStructHandler((export, structType) => export.AddDeserializableStruct(structType))
		);
	}

	/// <summary>
	/// UhtType のツリーを再帰的に探索して、Enumに対するSerialize/Deserialize対象を収集する。
	/// </summary>
	private static void CollectEnumExports(
		UhtSession session,
		UhtType type,
		Dictionary<UhtHeaderFile, TypesInHeader> exportMap
	)
	{
		CollectExports<UhtEnum>(
			session,
			type,
			exportMap,
			CreateEnumHandler((export, enumRecord) => export.AddSerializableEnum(enumRecord)),
			CreateEnumHandler((export, enumRecord) => export.AddDeserializableEnum(enumRecord))
		);
	}

	/// <summary>
	/// 再帰的にUhtTypeを辿りながら、T で指定した型のエクスポートを収集する共通処理。
	/// </summary>
	private static void CollectExports<T>(
		UhtSession session,
		UhtType type,
		Dictionary<UhtHeaderFile, TypesInHeader> exportMap,
		Action<TypesInHeader, T> addSerializable,
		Action<TypesInHeader, T> addDeserializable
	)
		where T : UhtType
	{
		// 指定型でキャストできないなら子要素を再帰的に見る
		if (type is not T tType)
		{
			foreach (UhtType child in type.Children)
			{
				CollectExports(session, child, exportMap, addSerializable, addDeserializable);
			}

			return;
		}

		bool isSerializable = type.MetaData.ContainsKey("Serialize");
		bool isDeserializable = type.MetaData.ContainsKey("Deserialize");

		if (!isSerializable && !isDeserializable)
		{
			return;
		}

		if (!exportMap.TryGetValue(tType.HeaderFile, out TypesInHeader? export))
		{
			export = new TypesInHeader();
			exportMap[tType.HeaderFile] = export;
		}

		if (isSerializable)
		{
			addSerializable(export, tType);
		}

		if (isDeserializable)
		{
			addDeserializable(export, tType);
		}
	}

	#endregion

	#region Factory Methods (StructRecord / EnumRecord)

	private static Action<TypesInHeader, UhtStruct> CreateStructHandler(Action<TypesInHeader, StructRecord> addAction)
	{
		return (export, structType) =>
		{
			FieldNameCase? renameAll = GetRenameAll(structType);
			var record = new StructRecord(structType, renameAll);
			addAction(export, record);
		};
	}

	private static Action<TypesInHeader, UhtEnum> CreateEnumHandler(Action<TypesInHeader, EnumRecord> addAction)
	{
		return (export, enumType) =>
		{
			FieldNameCase? renameAll = GetRenameAll(enumType);
			var record = new EnumRecord(renameAll, enumType);
			addAction(export, record);
		};
	}

	#endregion
}