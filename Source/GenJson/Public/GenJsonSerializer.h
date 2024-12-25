#pragma once

#include "GenJsonDeserializerImpls.h"
#include "GenJsonSerializerImpls.h"
#include "RapidJsonType.h"

namespace GenJson
{
	using FSerializerFuncRef = TFunctionRef<bool(const void* StructInstance, FJsonWriter& Writer)>;

	GENJSON_API inline TMap<const UScriptStruct*, FSerializerFuncRef>& GetStructSerializers()
	{
		static TMap<const UScriptStruct*, FSerializerFuncRef> StructSerializers;
		return StructSerializers;
	}

	GENJSON_API void RegisterStructSerializer(const UScriptStruct* StructType,
	                                          const FSerializerFuncRef& SerializerFunc);

	template <typename T>
	FORCEINLINE bool Write(const T& StructInstance, FJsonWriter& Writer)
	{
		return TSerializer<T>::Write(StructInstance, Writer);
	}
	
	FORCEINLINE bool Write(const TCHAR* Chars, FJsonWriter& Writer)
	{
		return TSerializer<TCHAR*>::Write(Chars, Writer);
	}

	template <typename T>
	FORCEINLINE bool Read(T& StructInstance, const FJsonReader& Reader)
	{
		return TDeserializer<T>::Read(StructInstance, Reader);
	}

	// it's not working
	GENJSON_API bool Write(const UScriptStruct* StructType, const void* StructInstance, FJsonWriter& Writer);
	
	template <typename T>
	FORCEINLINE bool Write_BPPath(const void* StructInstance, FJsonWriter& Writer)
	{
		return TSerializer<T>::Write(*static_cast<const T*>(StructInstance), Writer);
	}
}

#define GENJSON_REGISTER_STRUCT_SERIALIZER(Type) \
	static auto AutoRegister##Type##Serializer = []() \
	{ \
		GenJson::RegisterStructSerializer(Type::StaticStruct(), GenJson::Write_BPPath<Type>); \
		return true; \
	}();
