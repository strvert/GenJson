#pragma once

#include "RapidJsonIncludes.h"

namespace GenJson
{
	using FRapidJsonStringBuffer = rapidjson::GenericStringBuffer<rapidjson::UTF8<>>;

	using FJsonWriter = rapidjson::Writer<FRapidJsonStringBuffer, rapidjson::UTF16<>>;
	using FJsonReader = rapidjson::GenericReader<rapidjson::UTF8<>, rapidjson::UTF16<>>;

	inline FUtf8StringView ToUtf8String(const FRapidJsonStringBuffer& Buffer)
	{
		return FUtf8StringView(Buffer.GetString(), Buffer.GetSize());
	}

	inline FString ToString(const FRapidJsonStringBuffer& Buffer)
	{
		return FString(StringCast<TCHAR>(ToUtf8String(Buffer).GetData()).Get());
	}
}

// template <typename Encoding, typename Allocator = CrtAllocator>
// class GenericStringBuffer {
// public:
// 	typedef typename Encoding::Ch Ch;
//
// 	GenericStringBuffer(Allocator* allocator = 0, size_t capacity = kDefaultCapacity) : stack_(allocator, capacity) {}
//
// #if RAPIDJSON_HAS_CXX11_RVALUE_REFS
// 	GenericStringBuffer(GenericStringBuffer&& rhs) : stack_(std::move(rhs.stack_)) {}
// 	GenericStringBuffer& operator=(GenericStringBuffer&& rhs) {
// 		if (&rhs != this)
// 			stack_ = std::move(rhs.stack_);
// 		return *this;
// 	}
// #endif
//
// 	void Put(Ch c) { *stack_.template Push<Ch>() = c; }
// 	void PutUnsafe(Ch c) { *stack_.template PushUnsafe<Ch>() = c; }
// 	void Flush() {}
//
// 	void Clear() { stack_.Clear(); }
// 	void ShrinkToFit() {
// 		// Push and pop a null terminator. This is safe.
// 		*stack_.template Push<Ch>() = '\0';
// 		stack_.ShrinkToFit();
// 		stack_.template Pop<Ch>(1);
// 	}
//
// 	void Reserve(size_t count) { stack_.template Reserve<Ch>(count); }
// 	Ch* Push(size_t count) { return stack_.template Push<Ch>(count); }
// 	Ch* PushUnsafe(size_t count) { return stack_.template PushUnsafe<Ch>(count); }
// 	void Pop(size_t count) { stack_.template Pop<Ch>(count); }
//
// 	const Ch* GetString() const {
// 		// Push and pop a null terminator. This is safe.
// 		*stack_.template Push<Ch>() = '\0';
// 		stack_.template Pop<Ch>(1);
//
// 		return stack_.template Bottom<Ch>();
// 	}
//
// 	//! Get the size of string in bytes in the string buffer.
// 	size_t GetSize() const { return stack_.GetSize(); }
//
// 	//! Get the length of string in Ch in the string buffer.
// 	size_t GetLength() const { return stack_.GetSize() / sizeof(Ch); }
//
// 	static const size_t kDefaultCapacity = 256;
// 	mutable internal::Stack<Allocator> stack_;
//
// private:
// 	// Prohibit copy constructor & assignment operator.
// 	GenericStringBuffer(const GenericStringBuffer&);
// 	GenericStringBuffer& operator=(const GenericStringBuffer&);
// };
