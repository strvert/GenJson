#pragma once
#include "RapidJsonType.h"

namespace GenJson
{
	template <typename T, typename = void>
	struct TSerializer
	{
		static bool Write(const T&, FJsonWriter&)
		{
			static_assert(
				sizeof(T) == 0,
				"TJsonStructSerializer not specialized for this type."
				"Please provide a specialization via UHT-generated code or by including a custom serializer.");
			return false;
		}
	};

	template <>
	struct TSerializer<FStringView>
	{
		static FORCEINLINE bool Write(const FStringView Value, FJsonWriter& Writer)
		{
			return Writer.String(Value.GetData(), Value.Len());
		}
	};

	template <>
	struct TSerializer<TCHAR*>
	{
		static FORCEINLINE bool Write(const TCHAR* Value, FJsonWriter& Writer)
		{
			return TSerializer<FStringView>::Write(Value, Writer);
		}
	};

	template <>
	struct TSerializer<FString>
	{
		static FORCEINLINE bool Write(const FString& Value, FJsonWriter& Writer)
		{
			return TSerializer<FStringView>::Write(Value, Writer);
		}
	};

	template <>
	struct TSerializer<FName>
	{
		static FORCEINLINE bool Write(const FName& Value, FJsonWriter& Writer)
		{
			return TSerializer<FStringView>::Write(Value.ToString(), Writer);
		}
	};

	template <>
	struct TSerializer<FText>
	{
		static FORCEINLINE bool Write(const FText& Value, FJsonWriter& Writer)
		{
			return TSerializer<FStringView>::Write(Value.ToString(), Writer);
		}
	};

	template <>
	struct TSerializer<uint8>
	{
		static FORCEINLINE bool Write(const uint8& Value, FJsonWriter& Writer)
		{
			return Writer.Uint(Value);
		}
	};

	template <>
	struct TSerializer<int32>
	{
		static FORCEINLINE bool Write(const int32& Value, FJsonWriter& Writer)
		{
			return Writer.Int(Value);
		}
	};

	template <>
	struct TSerializer<int64>
	{
		static FORCEINLINE bool Write(const int64& Value, FJsonWriter& Writer)
		{
			return Writer.Int64(Value);
		}
	};

	template <>
	struct TSerializer<bool>
	{
		static FORCEINLINE bool Write(const bool& Value, FJsonWriter& Writer)
		{
			return Writer.Bool(Value);
		}
	};

	template <>
	struct TSerializer<float>
	{
		static FORCEINLINE bool Write(const float& Value, FJsonWriter& Writer)
		{
			return Writer.Double(Value);
		}
	};

	template <>
	struct TSerializer<double>
	{
		static FORCEINLINE bool Write(const double& Value, FJsonWriter& Writer)
		{
			return Writer.Double(Value);
		}
	};

	template <>
	struct TSerializer<FDateTime>
	{
		static FORCEINLINE bool Write(const FDateTime& Value, FJsonWriter& Writer)
		{
			return TSerializer<FStringView>::Write(Value.ToString(), Writer);
		}
	};

	template <typename ElementType>
	struct TSerializer<TArrayView<ElementType>>
	{
		static bool Write(const TArrayView<const ElementType>& Array, FJsonWriter& Writer)
		{
			Writer.StartArray();
			for (const ElementType& Element : Array)
			{
				if (!TSerializer<ElementType>::Write(Element, Writer))
				{
					return false;
				}
			}
			Writer.EndArray();
			return true;
		}
	};

	template <>
	struct TSerializer<FLinearColor>
	{
		static FORCEINLINE bool Write(const FLinearColor& Value, FJsonWriter& Writer)
		{
			const float RGBA[4] = {Value.R, Value.G, Value.B, Value.A};
			return TSerializer<TArrayView<float>>::Write(TArrayView<const float>(RGBA), Writer);
		}
	};

	template <>
	struct TSerializer<FColor>
	{
		static FORCEINLINE bool Write(const FColor& Value, FJsonWriter& Writer)
		{
			const uint8 RGBA[4] = {Value.R, Value.G, Value.B, Value.A};
			return TSerializer<TArrayView<uint8>>::Write(TArrayView<const uint8>(RGBA), Writer);
		}
	};

	template <typename BaseType>
	struct TSerializer<UE::Math::TVector<BaseType>>
	{
		static FORCEINLINE bool Write(const UE::Math::TVector<BaseType>& Value, FJsonWriter& Writer)
		{
			return TSerializer<TArrayView<BaseType>>::Write(Value.ArrayView(), Writer);
		}
	};

	template <typename ElementType>
	struct TSerializer<TSet<ElementType>>
	{
		static bool Write(const TSet<ElementType>& Set, FJsonWriter& Writer)
		{
			return TSerializer<TArrayView<ElementType>>::Write(Set.ArrayView(), Writer);
		}
	};

	template <typename ElementType>
	struct TSerializer<TOptional<ElementType>>
	{
		static bool Write(const TOptional<ElementType>& Value, FJsonWriter& Writer)
		{
			if (Value.IsSet())
			{
				return TSerializer<ElementType>::Write(*Value, Writer);
			}
			return Writer.Null();
		}
	};

	template <typename ElementType>
	struct TSerializer<TArray<ElementType>>
	{
		static bool Write(const TArray<ElementType>& Array, FJsonWriter& Writer)
		{
			return TSerializer<TArrayView<ElementType>>::Write(Array, Writer);
		}
	};

	template <typename KeyType, typename ValueType>
	struct TSerializer<TMap<KeyType, ValueType>>
	{
		static bool Write(const TMap<KeyType, ValueType>& Map, FJsonWriter& Writer)
		{
			Writer.StartObject();
			for (const auto& Pair : Map)
			{
				if (!TSerializer<KeyType>::Write(Pair.Key, Writer))
				{
					return false;
				}
				if (!TSerializer<ValueType>::Write(Pair.Value, Writer))
				{
					return false;
				}
			}
			Writer.EndObject();
			return true;
		}
	};
}
