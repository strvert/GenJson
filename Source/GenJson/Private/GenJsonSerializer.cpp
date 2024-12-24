#include "GenJsonSerializer.h"

void GenJson::RegisterStructSerializer(const UScriptStruct* StructType, const FSerializerFuncRef& SerializerFunc)
{
	GetStructSerializers().Add(StructType, SerializerFunc);
}

bool GenJson::Write(const UScriptStruct* StructType, const void* StructInstance, FJsonWriter& Writer)
{
	if (const FSerializerFuncRef* SerializerFunc = GetStructSerializers().Find(StructType))
	{
		(*SerializerFunc)(StructInstance, Writer);
		return true;
	}

	return false;
}
