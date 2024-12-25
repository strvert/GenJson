#pragma once
#include "RapidJsonType.h"

namespace GenJson
{
	template <typename T, typename = void>
	struct TDeserializer
	{
		static bool Read(const T&, FJsonReader&)
		{
			static_assert(
				sizeof(T) == 0,
				"TJsonStructDeserializer not specialized for this type."
				"Please provide a specialization via UHT-generated code or by including a custom deserializer.");
			return false;
		}
	};
}
