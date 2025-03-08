![Hackathon Logo](docs/images/hackathon.png?raw=true "Hackathon Logo")
# Sitecore Hackathon 2025

- MUST READ: **[Submission requirements](SUBMISSION_REQUIREMENTS.md)**
  
# Hackathon Submission Entry form

## Team name
⟹ Plan Fénix

## Category
⟹ Free

## Description  
  
### Module Purpose  
⟹ MediaMatchEngine is an AI-powered semantic taxonomy generator that bridges the gap between page content and relevant images in the Sitecore Media Library. It analyzes page content using advanced language models to extract contextual meaning and generate appropriate taxonomy labels that can be used to discover and match visually relevant media assets.  
  
### Problem Solved  
Content authors frequently struggle to find appropriate images that semantically match their content without manually browsing through extensive media libraries or relying on exact keyword matches that often miss contextually relevant assets.  
  
### How this module solves it  
MediaMatchEngine solves this challenge by:  
1. Automatically analyzing page content to understand its semantic meaning  
2. Generating a rich set of taxonomy labels that capture the content's context, themes, and subject matter  
3. Outputting these labels in a structured JSON format that integrates with Sitecore's asset library  
4. Enabling content authors to discover visually relevant images based on the semantic meaning of their content rather than exact keyword matches  
5. Streamlining the content creation workflow by reducing time spent searching for appropriate visual assets  
  
This creates a more intuitive connection between content and imagery, improving both author experience and content quality.

## Video link
⟹ [MediaMatchEngine Demo](#video-link)

## Pre-requisites and Dependencies
- Sitecore SXA
- Sitecore Headless Services for Sitecore XP
- .NET Core 8.0 API that is already hosted in Azure App Services (connects to our AI Agent for content analysis and taxonomy label generation)
- Node.js 18.18+

## Installation instructions
### Component 1: Plan Fénix Sitecore Module
To install the Plan Fénix Tool, go to the Sitecore Installation Wizard, and select the zip file in the folder 2025-Plan-Fenix\Packages\plan-fenix-module-1.zip (from the git source control). The file contains the templates and script modules, for the sample content install the file plan-fenix-sample-content-1.zip.

The only restriction is that the new pages needs to inherit from the PlanFenixTemplate, because the script validates this inheritance. 

### Component 2: .NET Core 8.0 API (Pre-hosted in Azure)    
1. **No installation required** - The .NET Core 8.0 API is already hosted in Azure App Services and all references in the solution point to this instance.  
  
2. **For reference only** - The API code is available in the GitHub repository at `src/feature/api/IAContentAnalyzer` if you wish to review it.

### Component 3: Headless Website (Node.js)

1. **Clone the repository**:
   ```bash
   git clone https://github.com/Sitecore-Hackathon/2025-Plan-Fenix.git
   ```

2. **Navigate to the frontend directory**:
   ```bash
   cd src/frontend
   ```

3. **Install dependencies**:
   ```bash
   npm i
   ```

4. **Start the development server**:
   ```bash
   npm run dev
   ```

5. **Access the demo page**:
   - Open your browser and navigate to: `http://localhost:[port]?page=/path/to/your/sitecore/page`
   - Replace `/path/to/your/sitecore/page` with the actual page path you want to visualize
   - The website will display the page content along with suggested images based on the generated taxonomy labels

### Configuration
### Headless Website Environment Variables

You need to create an environment file at `src/frontend/.env` with the following parameters:

```
VITE_API_URL=YOUR_SITECORE_INSTANCE_DOMAIN
VITE_SITECORE_API_KEY=YOUR_SITECORE_API_KEY
```

Replace:
- `YOUR_SITECORE_INSTANCE_DOMAIN` with your Sitecore instance URL (e.g., https://mysitecore.example.com)
- `YOUR_SITECORE_API_KEY` with your Sitecore API key that has permissions to access the Media Library

Example of a completed `.env` file:
```
VITE_API_URL=https://hackathon2023.sitecore.example.com
VITE_SITECORE_API_KEY={A1B2C3D4-E5F6-G7H8-I9J0-K1L2M3N4O5P6}
```

> Note: The `.env` file is excluded from version control for security reasons. You must create this file manually after cloning the repository.

## Usage instructions

MediaMatchEngine seamlessly integrates with your Sitecore content authoring workflow to suggest relevant images based on page content. Here's how to use it:

### Step 1: Create a new page from template
1. Navigate to your content tree in Sitecore
2. Right-click on the desired location and select **Insert from template**
   
![Insert from template](https://github.com/user-attachments/assets/8934e52b-f39a-4b06-b4bf-be0f8426dca1)

### Step 2: Select the PlanFenixTemplate
1. Browse to **Feature > PlanFenixTemplate**
2. Select the template to create your new page

![Select the PlanFenixTemplate](https://github.com/user-attachments/assets/918e5d6f-1474-414d-8d74-18adbf2f36cc)

### Step 3: Create your content page
1. Fill in the page title (e.g., "Microplastics are everywhere")
2. Add your content in the Content field

![Create your content page](https://github.com/user-attachments/assets/f45e2a55-e29f-4770-9b17-08faeb422085)

### Step 4: Generate semantic tags and image suggestions
1. Right-click on your content item in the content tree
2. Navigate to **Scripts > Plan Fenix > Execution**

![Generate semantic tags and image suggestions](https://github.com/user-attachments/assets/8535538a-fd71-411b-b661-8fd0753404f6)

### Step 5: Wait for the script to process
1. The system will analyze your content using the MediaMatchEngine
2. A progress indicator will show while processing

![Wait for the script to process](https://github.com/user-attachments/assets/d7f3df76-7aa3-4e08-b191-11710d97b961)

### Step 6: Review generated tags
1. Once processing completes, the "Generated Content" section will display
2. Review the semantic tags automatically extracted from your content
3. These tags represent the key concepts and themes in your content

![Review generated tags](https://github.com/user-attachments/assets/490d9ce0-a6c0-499e-8b44-c34859d5972b)

### Step 7: View suggested images
1. The system automatically matches images from your Media Library based on the generated tags
2. Relevant images appear in the "Images" section below the tags
3. You can select all or individual images to associate with your content

### Step 8: Preview the page with suggested images
1. Access your page through the headless frontend
2. The page will display with the suggested images that match your content
3. Images are presented in a visually appealing gallery above your content

![Preview the page with suggested images](https://github.com/user-attachments/assets/36d47ce1-238c-4d88-a970-c1378b7414e3)

### Comments
- The system uses AI to analyze your content and generate relevant taxonomy tags
- Tags are matched against your Sitecore Media Library metadata
- The more descriptive your content, the better the image suggestions will be
- You can refresh the suggestions by running the script again if you update your content
