#pragma once

#include "CoreMinimal.h"
#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

DECLARE_LOG_CATEGORY_EXTERN(LogGameInputLab, Log, All);

namespace GILJson
{
	/** Parse a JSON object. Logs an error and returns false for anything else. */
	inline bool ParseObject(const FString& Text, TSharedPtr<FJsonObject>& OutObject)
	{
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Text);
		if (!FJsonSerializer::Deserialize(Reader, OutObject) || !OutObject.IsValid())
		{
			UE_LOG(LogGameInputLab, Error, TEXT("Game Input Lab: the JSON is empty or not an object."));
			return false;
		}
		return true;
	}

	inline double Number(const FJsonObject& Data, const TCHAR* Key, double Default)
	{
		double Value = Default;
		return Data.TryGetNumberField(Key, Value) ? Value : Default;
	}

	inline FString String(const FJsonObject& Data, const TCHAR* Key, const FString& Default = FString())
	{
		FString Value;
		return Data.TryGetStringField(Key, Value) ? Value : Default;
	}

	/** The object under Key, or an empty one when it is missing or not an object. */
	inline const FJsonObject& Object(const FJsonObject& Data, const TCHAR* Key)
	{
		static FJsonObject Empty;
		const TSharedPtr<FJsonObject>* Value = nullptr;
		return (Data.TryGetObjectField(Key, Value) && Value && Value->IsValid()) ? **Value : Empty;
	}
}
