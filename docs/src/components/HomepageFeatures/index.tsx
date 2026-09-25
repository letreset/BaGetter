import Link from '@docusaurus/Link';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  icon: string;
  title: string;
  to: string;
  description: string;
};

const FeatureList: FeatureItem[] = [
  {
    icon: '🗂️',
    title: 'Multiple feeds',
    to: '/docs/feeds',
    description: 'Separate feeds on one server, each with its own packages, overwrite and deletion rules, retention and size limit.',
  },
  {
    icon: '🔐',
    title: 'Users and permissions',
    to: '/docs/authentication',
    description: 'Local accounts, Microsoft Entra ID sign-in, groups synced from app roles, and pull, push and delete permissions per feed.',
  },
  {
    icon: '🔑',
    title: 'Personal access tokens',
    to: '/docs/authentication#personal-access-tokens-pats',
    description: 'Tokens for CI and NuGet clients, with a maximum lifetime and email reminders before they expire.',
  },
  {
    icon: '🪞',
    title: 'Read-through mirrors',
    to: '/docs/feeds#mirror-read-through-cache',
    description: 'Cache nuget.org or any other NuGet feed per feed, with basic, bearer or custom header authentication.',
  },
  {
    icon: '☁️',
    title: 'Pluggable backends',
    to: '/docs/configuration#database-configuration',
    description: 'SQLite, SQL Server, PostgreSQL or MySQL, and the file system, Azure Blob, AWS S3, Google Cloud, Aliyun or Tencent storage.',
  },
  {
    icon: '🚀',
    title: 'Easy to deploy',
    to: '/docs/Installation/docker',
    description: 'Multi-arch Docker image, a Helm chart on GHCR, or a zip that runs anywhere .NET runs, including behind IIS.',
  },
];

function Feature({icon, title, to, description}: FeatureItem) {
  return (
    <div className="col col--4">
      <Link className={styles.card} to={to}>
        <span className={styles.icon} aria-hidden="true">{icon}</span>
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </Link>
    </div>
  );
}

export default function HomepageFeatures(): JSX.Element {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props) => (
            <Feature key={props.title} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
