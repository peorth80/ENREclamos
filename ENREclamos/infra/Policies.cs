namespace ENREclamos.Infrastructure;

public class Policies
{
	internal static string readPolicyForReclamosTable(string dynamodbTable)
	{
		var policyText = $@"{{{{
			""Version"": ""2012-10-17"",
			""Statement"": [{{{{
				""Effect"": ""Allow"",
				""Action"": [
					""dynamodb:DescribeTable"",
					""dynamodb:Scan"",
					""dynamodb:UpdateItem"",
					""dynamodb:PutItem"",
					""dynamodb:BatchWriteItem""
				],
				""Resource"": ""{dynamodbTable}""
			}}}}]
		}}}}";

		var parsedText = string.Format(policyText, dynamodbTable);

		return parsedText;
	}

	internal static string readAsumePolicyForLambda()
	{
		var policyText = @"{
			""Version"": ""2012-10-17"",
			""Statement"": [
				{
					""Action"": ""sts:AssumeRole"",
					""Principal"": {
						""Service"": ""lambda.amazonaws.com""
					},
					""Effect"": ""Allow"",
					""Sid"": """"
				}
			]
		}";

		return policyText;
	}

	internal static string readAsumePolicyForSchedule()
	{
		var policyText = @"{
					""Version"": ""2012-10-17"",
					""Statement"": [
						{
							""Action"": ""sts:AssumeRole"",
							""Principal"": {
								""Service"": ""scheduler.amazonaws.com""
							},
							""Effect"": ""Allow"",
							""Sid"": """"
						}
					]
				}";

		return policyText;
	}

	internal static string readPolicyForSchedule(string functionName)
	{
		var current = Pulumi.Aws.GetCallerIdentity.InvokeAsync();

		var region = "us-east-1"; //TODO dynamic
		var accountId = current.Result.AccountId;

		var policyText = @"{{
			""Version"": ""2012-10-17"",
			""Statement"": [
			{{
				""Effect"": ""Allow"",
				""Action"": [
				""lambda:InvokeFunction""
				],
				""Resource"": [
				""arn:aws:lambda:{0}:{1}:function:{2}:*"",
				""arn:aws:lambda:{0}:{1}:function:{2}""
				]
			}}
			]
		}}";

		var parsedText = string.Format(policyText, region, accountId, functionName);

		return parsedText;
	}

	internal static string readPolicyForCloudwatch()
	{
		var policyText = @"{
			""Version"": ""2012-10-17"",
			""Statement"": [{
				""Effect"": ""Allow"",
				""Action"": [
					""logs:CreateLogGroup"",
					""logs:CreateLogStream"",
					""logs:PutLogEvents""
				],
				""Resource"": ""arn:aws:logs:*:*:*""
			}]
		}";

		return policyText;
	}

	internal static string readPolicyForS3Bucket(string bucketName)
	{
		var policyText = $@"{{
			""Version"": ""2012-10-17"",
			""Statement"": [{{
				""Effect"": ""Allow"",
				""Action"": [
					""s3:PutObject"",
					""s3:GetObject"",
					""s3:DeleteObject""
				],
				""Resource"": ""{bucketName}/*""
			}}]
		}}";

		return policyText;
	}
}