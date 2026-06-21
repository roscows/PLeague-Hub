import type { ForumComment, ForumCommentNode } from '../types/api';

function compareComments(left: ForumCommentNode, right: ForumCommentNode) {
  const dateDifference = new Date(left.datumKreiranja).getTime() - new Date(right.datumKreiranja).getTime();
  return dateDifference || left.broj - right.broj;
}

export function buildCommentTree(comments: ForumComment[]): ForumCommentNode[] {
  const nodes = new Map<string, ForumCommentNode>();

  comments.forEach((comment) => {
    nodes.set(comment.id, { ...comment, children: [], depth: 1 });
  });

  const roots: ForumCommentNode[] = [];

  nodes.forEach((node) => {
    const parent = node.parentCommentId ? nodes.get(node.parentCommentId) : undefined;

    if (parent && parent.id !== node.id) {
      parent.children.push(node);
    } else {
      roots.push(node);
    }
  });

  function assignDepth(node: ForumCommentNode, parentDepth: number) {
    node.depth = parentDepth + 1;
    node.children.sort(compareComments);
    node.children.forEach((child) => assignDepth(child, node.depth));
  }

  roots.sort(compareComments);
  roots.forEach((root) => {
    root.depth = 1;
    root.children.sort(compareComments);
    root.children.forEach((child) => assignDepth(child, 1));
  });

  return roots;
}

export function partitionPinnedThreads(nodes: ForumCommentNode[]) {
  const pinned: ForumCommentNode[] = [];
  const regular: ForumCommentNode[] = [];

  for (const node of nodes) {
    if (node.istaknut) {
      pinned.push(withDepth(node, 1));
      continue;
    }

    const children = partitionPinnedThreads(node.children);
    pinned.push(...children.pinned);
    regular.push({ ...node, children: children.regular });
  }

  return { pinned, regular };
}

function withDepth(node: ForumCommentNode, depth: number): ForumCommentNode {
  return {
    ...node,
    depth,
    children: node.children.map((child) => withDepth(child, depth + 1))
  };
}
