# Unily Content Export

This project is a utility tool for exporting content from the Unily platform using the available search API and GraphQL queries. It provides a command-line interface (CLI) for exporting various types of content, such as pages, or articles.

## Prerequisites

Before running this tool, ensure that you have the following prerequisites installed on your machine:

- .NET 8 SDK

- Unily API credentials

## Installation

1.**Clone this repository to your local machine.**

```bash

git clone https://github.com/your-repo/UnilyContentExport.git
cd UnilyContentExport

```

2.**Open the project in Visual Studio:**

  * Launch Visual Studio, Rider or .Net IDE of choice.

  * Open/Load the UnilyContentExport solution file.

## Usage

To export content from the Unily platform, follow these steps:

1.Create your GraphQL queries and insert them as a string array of 'Queries' in the GraphQL section.

2.The query needs to either fetch all content, or have the paging scheme inserted if the content count is greater than 10'000 items.

To be able to do paging, the code will look for **{lastId}** and **{take}** in the query. It works by doing skip take logic by id in a descending order.

**Full Query Example:**

```graphql
query GetNewsWithViewCount {
  content {
    byQueryText(queryText: "+nodeTypeAlias:news +id:[0 TO {lastId}]", sort: {field: "id", direction: "desc"}, take: {take}) {
      totalRows
      data {
        id
        path
        nodeName
        properties {
          ... on StoryInterface {
            postImage {
              mediaUrl
            }
            author {
              displayName
              email
            }
            title
            description
            pageContent
          }
        }
      }
    }
  }
}
```

From the full example, the part for "0 TO 999999" of the id, the 999999 needs to be replaced with "{lastId}" and the "1000" for the take to be replace with "{take}", so the query in the config has:

byQueryText(queryText`: "`+nodeTypeAlias:News +id:`[`0 TO ==**{lastId}**==]", sort: {field: "id", direction: "desc"}, take: **=={take}==**)

instead of

byQueryText(queryText`: "`+nodeTypeAlias:News +id:`[`0 TO ==**999999**==]", sort: {field: "id", direction: "desc"}, take: **==1000==**)


3.The exported content will be saved in the specified output directory.

## Configuration

To configure the Unily API credentials, open the `appsettings.json` file in the root directory of the project and update the `UnilyApi` section with your credentials.

To export content you will require a Unily Application to be registered with the scope of "gateway.graphql". If yoyu have one already, it may be used.

In this example, two GQL queries were created to export News along with the post image and site pages. 

```text
The base item array must have the properties of id, path, and nodeName.
The mediaUrl if found in any queries will download locally to a single folder.
``` 

  * Add your Unily API credentials to the `appsettings.json` file
  * Escape the GQL Query for JSON. An editor like [Notepad++ with the escape plugin](https://github.com/RolandTaverner/npp-json-escape-unescape-plugin) works well for this as an example.

```json
{
    "Unily": {
        "ApiSiteUrl": "https://client-api.unily.com",
        "IdentityServiceUri": "https://client-idsrv.unily.com",
        "API": {
            "ClientId": "[registered app client id]",
            "ClientSecret": "[registered app client secrest]",
            "Scopes": "gateway.graphql"
        }
    },
    "GraphQl": {
        "BatchSize": 1000,
        "ParallelTasks": 2,
        "Endpoint": "/api/v1/search",
        "ExportPath": "D:\\UnilyExport\\Content",
        "ExportMediaPath": "D:\\UnilyExport\\Media",
        "UpperLimitId": 99999999,
        "Queries": {
            "News": "query GetNewsWithViewCount { content { byQueryText(queryText: \"+nodeTypeAlias:news +id:[0 TO {lastId}]\", sort: {field: \"id\", direction: \"desc\"}, take: {take}) { totalRows data { id path nodeName properties { ... on StoryInterface { postImage { mediaUrl } author { displayName email } title description pageContent } } } } }}",
            "SitePages": "query GetNodeTitleById { content{ byQueryText(queryText: \"+nodeTypeAlias:SitePage +id[0 TO {lastId}]\", sort: {field: \"id\", direction: \"desc\"}, take: {take}) { data { id path nodeName nodeTypeAlias url published lastModifiedDate site { id nodeName } properties { ... on SitePage { title hideInNavigation navigationLabel grid mobileGrid engageGrid engageMobileGrid } } } } } }"
        }
    }
}

```


## Contributing

Contributions are welcome! If you find any issues or have suggestions for improvements, please open an issue or submit a pull request.

## Disclaimer
The code provided in this project is **not developed, endorsed, or supported by Unily**. It is intended as an example of how to use Unily's APIs and GraphQL queries for content export, but **you are solely responsible for testing and validating its functionality** in your own environment.

By using this code, you acknowledge and agree that:

- **No warranty or liability** is provided. The project is offered "as-is," with no guarantees of accuracy, reliability, or performance.
- **Unily is not liable** for any issues or damages that arise from the use of this code.
- You are responsible for ensuring compliance with any relevant legal agreements or terms of service associated with Unily's APIs and any data processed through them.
- This tool **should not be used in a production environment** without thorough testing and review, as it may not cover all use cases or error conditions.
- **Modifications or extensions** to this code should be handled with caution and may require additional testing or adjustments based on specific requirements.
- It is your responsibility to ensure **data privacy and security** when exporting or handling any content.

Please note that this repository is **not actively monitored**, and no direct support is available. You are encouraged to fork the project and make any necessary adjustments for your own use.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more information.
