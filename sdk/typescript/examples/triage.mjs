import { PromptOne, triageQuestions } from 'promptone-client';

const promptone = new PromptOne({
  baseURL: process.env.PROMPTONE_BASE_URL ?? 'http://127.0.0.1:8000',
  apiKey: process.env.PROMPTONE_API_KEY,
  timeoutMs: 600_000,
});

try {
  const result = await promptone.predict(
    { message: 'I was billed twice for March. Please refund the duplicate today.' },
    triageQuestions(),
  );
  console.log(JSON.stringify(result, null, 2));
} catch (error) {
  console.error(error);
  process.exitCode = 1;
}
