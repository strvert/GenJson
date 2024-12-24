#pragma once

#include "RapidJsonIncludes.h"

namespace GenJson
{
	using FJsonWriterStringBuffer = rapidjson::GenericStringBuffer<rapidjson::UTF8<>>;
	using FJsonWriter = rapidjson::Writer<FJsonWriterStringBuffer, rapidjson::UTF16<>>;
}
